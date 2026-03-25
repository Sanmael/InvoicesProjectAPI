using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InvoicesProjectApplication.Services;

public class ChatService : IChatService
{
    private readonly IDebtService _debtService;
    private readonly IReceivableService _receivableService;
    private readonly ICreditCardService _creditCardService;
    private readonly ICardPurchaseService _cardPurchaseService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly List<ProviderConfig> _providers;
    private readonly ILogger<ChatService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private record ProviderConfig(
        string Name, string ApiKey, string Model, string BaseUrl,
        int? NumCtx, bool DisableThinking);

    public ChatService(
        IDebtService debtService,
        IReceivableService receivableService,
        ICreditCardService creditCardService,
        ICardPurchaseService cardPurchaseService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ChatService> logger)
    {
        _debtService = debtService;
        _receivableService = receivableService;
        _creditCardService = creditCardService;
        _cardPurchaseService = cardPurchaseService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _providers = BuildProviderChain(configuration);

        var names = string.Join(" → ", _providers.Select(p => $"{p.Name}({p.Model})"));
        logger.LogInformation("Chat provider chain: {Chain}", names);
    }

    private static List<ProviderConfig> BuildProviderChain(IConfiguration configuration)
    {
        var active = configuration["ChatProvider:Active"] ?? "Groq";
        var fallbacks = configuration.GetSection("ChatProvider:Fallbacks").Get<string[]>() ?? [];
        var chain = new[] { active }.Concat(fallbacks);

        var providers = new List<ProviderConfig>();
        foreach (var name in chain)
        {
            var section = configuration.GetSection($"ChatProvider:Providers:{name}");
            var apiKey = section["ApiKey"];
            var baseUrl = section["BaseUrl"];
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(baseUrl))
                continue;

            providers.Add(new ProviderConfig(
                name,
                apiKey,
                section["Model"] ?? "llama-3.3-70b-versatile",
                baseUrl,
                int.TryParse(section["NumCtx"], out var ctx) ? ctx : null,
                bool.TryParse(section["DisableThinking"], out var dt) && dt
            ));
        }

        return providers.Count > 0
            ? providers
            : throw new InvalidOperationException("Nenhum ChatProvider válido configurado.");
    }

    public async Task<ChatResponseDto> ProcessMessageAsync(Guid userId, ChatRequestDto request)
    {
        for (var i = 0; i < _providers.Count; i++)
        {
            var provider = _providers[i];
            var isLast = i == _providers.Count - 1;

            try
            {
                _logger.LogInformation("Tentando provider {Provider} ({Model})", provider.Name, provider.Model);
                return await CallProvider(userId, request, provider);
            }
            catch (HttpRequestException ex) when (!isLast && IsRateLimitOrUnavailable(ex))
            {
                _logger.LogWarning("Provider {Provider} indisponível ({Status}), tentando fallback...",
                    provider.Name, ex.StatusCode);
            }
        }

        return new ChatResponseDto("Todos os provedores de IA estão indisponíveis no momento. Tente novamente em alguns minutos.", null);
    }

    private static bool IsRateLimitOrUnavailable(HttpRequestException ex) =>
        ex.StatusCode is System.Net.HttpStatusCode.TooManyRequests
            or System.Net.HttpStatusCode.ServiceUnavailable
            or System.Net.HttpStatusCode.BadGateway
            or System.Net.HttpStatusCode.GatewayTimeout;

    private async Task<ChatResponseDto> CallProvider(Guid userId, ChatRequestDto request, ProviderConfig provider)
    {
        var messages = BuildMessages(request, provider.DisableThinking);
        var tools = GetToolDefinitions();

        var chatRequest = new
        {
            model = provider.Model,
            messages,
            tools,
            tool_choice = "auto",
            temperature = 0.1,
            max_tokens = 1500,
            options = provider.NumCtx.HasValue ? new { num_ctx = provider.NumCtx.Value } : (object?)null,
        };

        var client = _httpClientFactory.CreateClient("ChatProvider");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", provider.ApiKey);

        var response = await client.PostAsJsonAsync(provider.BaseUrl, chatRequest, JsonOptions);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GroqChatResponse>(JsonOptions);

        if (result?.Choices is null || result.Choices.Count == 0)
            return new ChatResponseDto("Não consegui processar sua mensagem. Tente novamente.", null);

        var choice = result.Choices[0];

        if (choice.Message?.ToolCalls is { Count: > 0 })
        {
            return await ExecuteToolCalls(userId, choice.Message.ToolCalls, messages, client, provider);
        }

        return new ChatResponseDto(
            choice.Message?.Content ?? "Não entendi. Pode reformular?", null);
    }

    private async Task<ChatResponseDto> ExecuteToolCalls(
        Guid userId,
        List<GroqToolCall> toolCalls,
        List<object> messages,
        HttpClient client,
        ProviderConfig provider)
    {
        var actions = new List<ChatActionResult>();
        var toolMessages = new List<object>();

        foreach (var toolCall in toolCalls)
        {
            var functionName = toolCall.Function?.Name ?? "";
            var argsJson = toolCall.Function?.Arguments ?? "{}";

            _logger.LogInformation("Executando tool: {Function} com args: {Args}", functionName, argsJson);

            string toolResult;
            try
            {
                toolResult = await ExecuteFunction(userId, functionName, argsJson, actions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao executar tool {Function}", functionName);
                toolResult = $"Erro: {ex.Message}";
                actions.Add(new ChatActionResult(functionName, ex.Message, false));
            }

            toolMessages.Add(new { role = "tool", tool_call_id = toolCall.Id, content = toolResult });
        }

        var followUp = new List<object>(messages);
        followUp.Add(new
        {
            role = "assistant",
            tool_calls = toolCalls.Select(tc => new
            {
                id = tc.Id,
                type = "function",
                function = new { name = tc.Function?.Name, arguments = tc.Function?.Arguments }
            })
        });
        followUp.AddRange(toolMessages);

        var followUpRequest = new
        {
            model = provider.Model,
            messages = followUp,
            temperature = 0.3,
            max_tokens = 1500,
            options = provider.NumCtx.HasValue ? new { num_ctx = provider.NumCtx.Value } : (object?)null,
        };

        var followUpResponse = await client.PostAsJsonAsync(provider.BaseUrl, followUpRequest, JsonOptions);

        if (followUpResponse.IsSuccessStatusCode)
        {
            var followUpResult = await followUpResponse.Content
                .ReadFromJsonAsync<GroqChatResponse>(JsonOptions);
            var reply = followUpResult?.Choices?.FirstOrDefault()?.Message?.Content;
            if (!string.IsNullOrWhiteSpace(reply))
                return new ChatResponseDto(reply, actions);
        }

        var summary = string.Join("\n", actions.Select(a =>
            a.Success ? $"✅ {a.Description}" : $"❌ {a.Description}"));
        return new ChatResponseDto(
            string.IsNullOrWhiteSpace(summary) ? "Ações processadas." : summary, actions);
    }

    private static DateOnly ParseDate(string dateStr) =>
        DateOnly.ParseExact(dateStr, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static DateTime ParseDateTimeUtc(string dateStr) =>
        DateTime.SpecifyKind(DateTime.Parse(dateStr), DateTimeKind.Utc);

    private async Task<Guid> ResolveCreditCardIdAsync(Guid userId, string identifier)
    {
        if (Guid.TryParse(identifier, out var cardId))
            return cardId;

        var cards = await _creditCardService.GetByUserIdAsync(userId);
        var match = cards.FirstOrDefault(c =>
            c.Name.Contains(identifier, StringComparison.OrdinalIgnoreCase) ||
            c.LastFourDigits == identifier);

        return match?.Id ?? throw new InvalidOperationException(
            $"Cartão '{identifier}' não encontrado. Use 'listar cartões' para ver seus cartões.");
    }

    private static Guid ParseGuidSafe(string value, string entityName)
    {
        if (Guid.TryParse(value, out var id))
            return id;
        throw new InvalidOperationException(
            $"ID inválido para {entityName}: '{value}'. Liste os itens primeiro para obter o ID correto.");
    }

    private async Task<string> ExecuteFunction(
        Guid userId, string functionName, string argsJson, List<ChatActionResult> actions)
    {
        switch (functionName)
        {
            // ─── DÉBITOS ───
            case "create_debt":
            {
                var args = JsonSerializer.Deserialize<CreateDebtArgs>(argsJson, JsonOptions)!;
                var dto = new CreateDebtDto(args.Description, args.Amount, ParseDate(args.DueDate), args.Notes);
                var debt = await _debtService.CreateAsync(userId, dto);
                actions.Add(new ChatActionResult("create_debt",
                    $"Débito '{debt.Description}' R${debt.Amount:F2} venc. {debt.DueDate:dd/MM/yyyy}", true));
                return $"Débito criado. ID: {debt.Id}, {debt.Description}, R${debt.Amount:F2}, venc. {debt.DueDate:dd/MM/yyyy}";
            }

            case "create_recurring_debt":
            {
                var args = JsonSerializer.Deserialize<CreateRecurringDebtArgs>(argsJson, JsonOptions)!;
                var startDate = !string.IsNullOrEmpty(args.StartMonth)
                    ? ParseDate($"{args.StartMonth}-01")
                    : (DateOnly?)null;
                var dto = new CreateRecurringDebtDto(args.Description, args.Amount, args.RecurringDay, args.Months, startDate, args.Notes);
                var debts = await _debtService.CreateRecurringAsync(userId, dto);
                var count = debts.Count();
                actions.Add(new ChatActionResult("create_recurring_debt",
                    $"{count} débitos recorrentes '{args.Description}' de R${args.Amount:F2} criados", true));
                return $"{count} débitos recorrentes criados: {args.Description}, R${args.Amount:F2}/mês, dia {args.RecurringDay}.";
            }

            case "edit_debt":
            {
                var args = JsonSerializer.Deserialize<EditDebtArgs>(argsJson, JsonOptions)!;
                var dto = new UpdateDebtDto(
                    args.Description,
                    args.Amount,
                    !string.IsNullOrEmpty(args.DueDate) ? ParseDate(args.DueDate) : null,
                    null,
                    args.Notes);
                var debt = await _debtService.UpdateAsync(ParseGuidSafe(args.Id, "débito"), dto);
                actions.Add(new ChatActionResult("edit_debt",
                    $"Débito '{debt.Description}' atualizado", true));
                return $"Débito atualizado: {debt.Description}, R${debt.Amount:F2}, venc. {debt.DueDate:dd/MM/yyyy}";
            }

            case "list_pending_debts":
            {
                var debts = await _debtService.GetPendingByUserIdAsync(userId);
                var list = debts.Take(15).Select(d =>
                    $"- [ID: {d.Id}] {d.Description}: R${d.Amount:F2} (venc. {d.DueDate:dd/MM/yyyy})");
                var result = list.Any()
                    ? $"Débitos pendentes:\n{string.Join("\n", list)}"
                    : "Nenhum débito pendente.";
                actions.Add(new ChatActionResult("list_pending_debts", "Listou débitos pendentes", true));
                return result;
            }

            // ─── RECEBÍVEIS ───
            case "create_receivable":
            {
                var args = JsonSerializer.Deserialize<CreateReceivableArgs>(argsJson, JsonOptions)!;
                var dto = new CreateReceivableDto(args.Description, args.Amount, ParseDate(args.ExpectedDate), args.Notes);
                var receivable = await _receivableService.CreateAsync(userId, dto);
                actions.Add(new ChatActionResult("create_receivable",
                    $"Recebível '{receivable.Description}' R${receivable.Amount:F2} previsão {receivable.ExpectedDate:dd/MM/yyyy}", true));
                return $"Recebível criado. ID: {receivable.Id}, {receivable.Description}, R${receivable.Amount:F2}, data {receivable.ExpectedDate:dd/MM/yyyy}";
            }

            case "create_recurring_receivable":
            {
                var args = JsonSerializer.Deserialize<CreateRecurringReceivableArgs>(argsJson, JsonOptions)!;
                var dto = new CreateRecurringReceivableDto(args.Description, args.Amount, args.RecurringDay, args.Notes, args.Months);
                var receivables = await _receivableService.CreateRecurringAsync(userId, dto);
                var count = receivables.Count();
                actions.Add(new ChatActionResult("create_recurring_receivable",
                    $"{count} recebíveis recorrentes '{args.Description}' de R${args.Amount:F2} criados", true));
                return $"{count} recebíveis recorrentes criados: {args.Description}, R${args.Amount:F2}/mês, dia {args.RecurringDay}.";
            }

            case "edit_receivable":
            {
                var args = JsonSerializer.Deserialize<EditReceivableArgs>(argsJson, JsonOptions)!;
                var dto = new UpdateReceivableDto(
                    args.Description,
                    args.Amount,
                    !string.IsNullOrEmpty(args.ExpectedDate) ? ParseDate(args.ExpectedDate) : null,
                    null,
                    args.Notes);
                var receivable = await _receivableService.UpdateAsync(ParseGuidSafe(args.Id, "recebível"), dto);
                actions.Add(new ChatActionResult("edit_receivable",
                    $"Recebível '{receivable.Description}' atualizado", true));
                return $"Recebível atualizado: {receivable.Description}, R${receivable.Amount:F2}, data {receivable.ExpectedDate:dd/MM/yyyy}";
            }

            case "list_pending_receivables":
            {
                var receivables = await _receivableService.GetPendingByUserIdAsync(userId);
                var list = receivables.Take(15).Select(r =>
                    $"- [ID: {r.Id}] {r.Description}: R${r.Amount:F2} (previsão {r.ExpectedDate:dd/MM/yyyy})");
                var result = list.Any()
                    ? $"Recebíveis pendentes:\n{string.Join("\n", list)}"
                    : "Nenhum recebível pendente.";
                actions.Add(new ChatActionResult("list_pending_receivables", "Listou recebíveis pendentes", true));
                return result;
            }

            // ─── CARTÕES DE CRÉDITO ───
            case "create_credit_card":
            {
                var args = JsonSerializer.Deserialize<CreateCreditCardArgs>(argsJson, JsonOptions)!;
                var dto = new CreateCreditCardDto(args.Name, args.LastFourDigits, args.CreditLimit, args.ClosingDay, args.DueDay);
                var card = await _creditCardService.CreateAsync(userId, dto);
                actions.Add(new ChatActionResult("create_credit_card",
                    $"Cartão '{card.Name}' final {card.LastFourDigits} criado", true));
                return $"Cartão criado. ID: {card.Id}, {card.Name}, final {card.LastFourDigits}, limite R${card.CreditLimit:F2}, fecha dia {card.ClosingDay}, vence dia {card.DueDay}";
            }

            case "edit_credit_card":
            {
                var args = JsonSerializer.Deserialize<EditCreditCardArgs>(argsJson, JsonOptions)!;
                var cardId = await ResolveCreditCardIdAsync(userId, args.Id);
                var dto = new UpdateCreditCardDto(args.Name, args.CreditLimit, args.ClosingDay, args.DueDay, args.IsActive);
                var card = await _creditCardService.UpdateAsync(cardId, dto);
                actions.Add(new ChatActionResult("edit_credit_card",
                    $"Cartão '{card.Name}' atualizado", true));
                return $"Cartão atualizado: {card.Name}, final {card.LastFourDigits}";
            }

            case "list_credit_cards":
            {
                var cards = await _creditCardService.GetByUserIdAsync(userId);
                var list = cards.Select(c =>
                    $"- [ID: {c.Id}] {c.Name} final {c.LastFourDigits} | Limite: R${c.CreditLimit:F2} | Fecha dia {c.ClosingDay} | Vence dia {c.DueDay} | Pendente: R${c.TotalPending:F2}");
                var result = list.Any()
                    ? $"Seus cartões:\n{string.Join("\n", list)}"
                    : "Nenhum cartão cadastrado.";
                actions.Add(new ChatActionResult("list_credit_cards", "Listou cartões", true));
                return result;
            }

            // ─── COMPRAS NO CARTÃO ───
            case "create_card_purchase":
            {
                var args = JsonSerializer.Deserialize<CreateCardPurchaseArgs>(argsJson, JsonOptions)!;
                var cardId = await ResolveCreditCardIdAsync(userId, args.CreditCardId);
                var dto = new CreateCardPurchaseDto(
                    cardId, args.Description, args.Amount,
                    ParseDateTimeUtc(args.PurchaseDate), args.Installments, args.Notes);
                var purchase = await _cardPurchaseService.CreateAsync(dto);
                actions.Add(new ChatActionResult("create_card_purchase",
                    $"Compra '{purchase.Description}' R${purchase.Amount:F2} em {purchase.Installments}x registrada", true));
                return $"Compra registrada. ID: {purchase.Id}, {purchase.Description}, R${purchase.Amount:F2}, {purchase.Installments}x";
            }

            case "edit_card_purchase":
            {
                var args = JsonSerializer.Deserialize<EditCardPurchaseArgs>(argsJson, JsonOptions)!;
                var dto = new UpdateCardPurchaseDto(
                    args.Description,
                    args.Amount,
                    !string.IsNullOrEmpty(args.PurchaseDate) ? ParseDateTimeUtc(args.PurchaseDate) : null,
                    args.Installments,
                    null,
                    args.Notes);
                var purchase = await _cardPurchaseService.UpdateAsync(ParseGuidSafe(args.Id, "compra"), dto);
                actions.Add(new ChatActionResult("edit_card_purchase",
                    $"Compra '{purchase.Description}' atualizada", true));
                return $"Compra atualizada: {purchase.Description}, R${purchase.Amount:F2}";
            }

            case "list_card_purchases":
            {
                var args = JsonSerializer.Deserialize<ListCardPurchasesArgs>(argsJson, JsonOptions)!;
                var cardId = await ResolveCreditCardIdAsync(userId, args.CreditCardId);
                var purchases = await _cardPurchaseService.GetPendingByCreditCardIdAsync(cardId);
                var list = purchases.Take(15).Select(p =>
                    $"- [ID: {p.Id}] {p.Description}: R${p.Amount:F2} ({p.CurrentInstallment}/{p.Installments}x) {p.PurchaseDate:dd/MM/yyyy}");
                var result = list.Any()
                    ? $"Compras pendentes:\n{string.Join("\n", list)}"
                    : "Nenhuma compra pendente neste cartão.";
                actions.Add(new ChatActionResult("list_card_purchases", "Listou compras do cartão", true));
                return result;
            }

            default:
                return $"Função '{functionName}' não reconhecida.";
        }
    }

    private static List<object> BuildMessages(ChatRequestDto request, bool disableThinking)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");
        var noThink = disableThinking ? "\n/no_think" : "";

        var messages = new List<object>
        {
            new
            {
                role = "system",
                content = $"""
                    Você é o Kash, assistente financeiro pessoal. Ajude o usuário a gerenciar débitos, recebíveis, cartões de crédito e compras.
                    Hoje é {today}. Mês atual: {currentMonth}. Ano atual: {DateTime.UtcNow.Year}.

                    REGRA PRINCIPAL DE CONFIRMAÇÃO:
                    - ANTES de executar qualquer ação que CRIA ou MODIFICA dados (criar débito, recebível, cartão, compra, editar qualquer coisa), você DEVE primeiro apresentar um resumo claro do que vai fazer e perguntar "Confirma?" ou similar.
                    - Só execute a ferramenta/tool quando o usuário confirmar com algo como "sim", "ok", "confirma", "pode fazer", "isso", "manda", "bora", etc.
                    - Se o usuário disser "não", "cancela", "errado", corrija ou pergunte o que mudar.
                    - Ações de LEITURA (listar débitos, listar cartões, etc.) podem ser executadas imediatamente sem confirmação.

                    REGRA DE LISTAGEM (OBRIGATÓRIA):
                    - Quando o usuário pedir para listar, ver, mostrar ou consultar itens (cartões, débitos, recebíveis, compras), você DEVE SEMPRE chamar a ferramenta correspondente (list_credit_cards, list_pending_debts, list_pending_receivables, list_card_purchases).
                    - NUNCA responda com base no histórico da conversa ou na sua memória. SEMPRE chame a ferramenta para buscar dados atualizados.
                    - Mesmo que você já tenha listado antes na mesma conversa, chame a ferramenta novamente.
                    - Ao apresentar o resultado de uma listagem, SEMPRE mostre TODOS os itens retornados pela ferramenta, um por um. NUNCA diga apenas "você tem X itens" sem listar quais são.

                    REGRA DE CONSULTA DE SALDO/LIMITE (OBRIGATÓRIA):
                    - Quando o usuário perguntar se pode comprar algo, quanto tem de limite, saldo disponível ou qualquer valor calculado, você DEVE chamar list_credit_cards ou list_card_purchases ANTES de responder.
                    - NUNCA faça cálculos de cabeça com valores do histórico da conversa. Os dados podem ter mudado.
                    - O limite disponível é: Limite total - Pendente (campo "Pendente" retornado pela ferramenta). Use APENAS esse valor.

                    Regras gerais:
                    - Se o usuário não informar o ano, assuma {DateTime.UtcNow.Year}.
                    - Se não informar o mês, assuma o mês atual ({currentMonth}).
                    - Valores devem ser números positivos.
                    - Datas no formato yyyy-MM-dd para as funções.
                    - Responda sempre em português brasileiro, de forma concisa e amigável.
                    - Use emojis moderadamente.
                    - Se o usuário pedir algo fora de finanças, diga educadamente que só trata de finanças.
                    - Para editar, o usuário pode referenciar itens pelo nome/descrição. Se ambíguo, liste os itens primeiro para ele escolher.
                    - Para compras no cartão, se o usuário não especificar qual cartão, liste os cartões disponíveis e peça para ele escolher.
                    {noThink}
                    """
            }
        };

        if (request.History is { Count: > 0 })
        {
            foreach (var msg in request.History.TakeLast(30))
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }
        }

        messages.Add(new { role = "user", content = request.Message });

        return messages;
    }

    private static List<object> GetToolDefinitions() =>
    [
        // ─── DÉBITOS ───
        Tool("create_debt",
            "Cria um novo débito simples (conta a pagar única).",
            new Dictionary<string, object>
            {
                ["description"] = new { type = "string", description = "Descrição do débito" },
                ["amount"] = new { type = "number", description = "Valor em reais" },
                ["due_date"] = new { type = "string", description = "Data de vencimento yyyy-MM-dd" },
                ["notes"] = new { type = "string", description = "Observações opcionais" },
            },
            ["description", "amount", "due_date"]),

        Tool("create_recurring_debt",
            "Cria débitos recorrentes mensais (ex: ajuda familiar, plano mensal). Gera N meses automaticamente.",
            new Dictionary<string, object>
            {
                ["description"] = new { type = "string", description = "Descrição" },
                ["amount"] = new { type = "number", description = "Valor mensal em reais" },
                ["recurring_day"] = new { type = "integer", description = "Dia do mês (1-28)" },
                ["months"] = new { type = "integer", description = "Quantidade de meses (1-60)" },
                ["start_month"] = new { type = "string", description = "Mês de início yyyy-MM (opcional, padrão mês atual)" },
                ["notes"] = new { type = "string", description = "Observações opcionais" },
            },
            ["description", "amount", "recurring_day", "months"]),

        Tool("edit_debt",
            "Edita um débito existente. Precisa do ID do débito. Só envie os campos que devem mudar.",
            new Dictionary<string, object>
            {
                ["id"] = new { type = "string", description = "ID (GUID) do débito" },
                ["description"] = new { type = "string", description = "Nova descrição (opcional)" },
                ["amount"] = new { type = "number", description = "Novo valor (opcional)" },
                ["due_date"] = new { type = "string", description = "Nova data yyyy-MM-dd (opcional)" },
                ["notes"] = new { type = "string", description = "Novas observações (opcional)" },
            },
            ["id"]),

        Tool("list_pending_debts",
            "Lista débitos pendentes (não pagos) com seus IDs.",
            new Dictionary<string, object>(), []),

        // ─── RECEBÍVEIS ───
        Tool("create_receivable",
            "Cria um recebível simples (valor a receber único).",
            new Dictionary<string, object>
            {
                ["description"] = new { type = "string", description = "Descrição do recebível" },
                ["amount"] = new { type = "number", description = "Valor em reais" },
                ["expected_date"] = new { type = "string", description = "Data prevista yyyy-MM-dd" },
                ["notes"] = new { type = "string", description = "Observações opcionais" },
            },
            ["description", "amount", "expected_date"]),

        Tool("create_recurring_receivable",
            "Cria recebíveis recorrentes mensais (ex: salário). Gera N meses automaticamente.",
            new Dictionary<string, object>
            {
                ["description"] = new { type = "string", description = "Descrição" },
                ["amount"] = new { type = "number", description = "Valor mensal em reais" },
                ["recurring_day"] = new { type = "integer", description = "Dia do mês (1-28)" },
                ["months"] = new { type = "integer", description = "Quantidade de meses (1-60)" },
                ["notes"] = new { type = "string", description = "Observações opcionais" },
            },
            ["description", "amount", "recurring_day", "months"]),

        Tool("edit_receivable",
            "Edita um recebível existente. Precisa do ID.",
            new Dictionary<string, object>
            {
                ["id"] = new { type = "string", description = "ID (GUID) do recebível" },
                ["description"] = new { type = "string", description = "Nova descrição (opcional)" },
                ["amount"] = new { type = "number", description = "Novo valor (opcional)" },
                ["expected_date"] = new { type = "string", description = "Nova data yyyy-MM-dd (opcional)" },
                ["notes"] = new { type = "string", description = "Novas observações (opcional)" },
            },
            ["id"]),

        Tool("list_pending_receivables",
            "Lista recebíveis pendentes (não recebidos) com seus IDs.",
            new Dictionary<string, object>(), []),

        // ─── CARTÕES DE CRÉDITO ───
        Tool("create_credit_card",
            "Cadastra um novo cartão de crédito.",
            new Dictionary<string, object>
            {
                ["name"] = new { type = "string", description = "Nome/apelido do cartão (ex: Nubank, Inter)" },
                ["last_four_digits"] = new { type = "string", description = "Últimos 4 dígitos do cartão" },
                ["credit_limit"] = new { type = "number", description = "Limite de crédito em reais (opcional)" },
                ["closing_day"] = new { type = "integer", description = "Dia de fechamento da fatura (1-28)" },
                ["due_day"] = new { type = "integer", description = "Dia de vencimento da fatura (1-28)" },
            },
            ["name", "last_four_digits", "closing_day", "due_day"]),

        Tool("edit_credit_card",
            "Edita um cartão de crédito existente. Pode usar o ID ou nome do cartão.",
            new Dictionary<string, object>
            {
                ["id"] = new { type = "string", description = "ID ou nome do cartão (ex: GUID ou 'Nubank')" },
                ["name"] = new { type = "string", description = "Novo nome (opcional)" },
                ["credit_limit"] = new { type = "number", description = "Novo limite (opcional)" },
                ["closing_day"] = new { type = "integer", description = "Novo dia de fechamento (opcional)" },
                ["due_day"] = new { type = "integer", description = "Novo dia de vencimento (opcional)" },
                ["is_active"] = new { type = "boolean", description = "Ativo ou inativo (opcional)" },
            },
            ["id"]),

        Tool("list_credit_cards",
            "Lista todos os cartões de crédito do usuário com IDs e detalhes.",
            new Dictionary<string, object>(), []),

        // ─── COMPRAS NO CARTÃO ───
        Tool("create_card_purchase",
            "Registra uma compra em um cartão de crédito.",
            new Dictionary<string, object>
            {
                ["credit_card_id"] = new { type = "string", description = "ID ou nome do cartão (ex: GUID ou 'Nubank')" },
                ["description"] = new { type = "string", description = "Descrição da compra" },
                ["amount"] = new { type = "number", description = "Valor total em reais" },
                ["purchase_date"] = new { type = "string", description = "Data da compra yyyy-MM-dd" },
                ["installments"] = new { type = "integer", description = "Número de parcelas (1 = à vista)" },
                ["notes"] = new { type = "string", description = "Observações opcionais" },
            },
            ["credit_card_id", "description", "amount", "purchase_date", "installments"]),

        Tool("edit_card_purchase",
            "Edita uma compra de cartão existente. Precisa do ID.",
            new Dictionary<string, object>
            {
                ["id"] = new { type = "string", description = "ID (GUID) da compra" },
                ["description"] = new { type = "string", description = "Nova descrição (opcional)" },
                ["amount"] = new { type = "number", description = "Novo valor (opcional)" },
                ["purchase_date"] = new { type = "string", description = "Nova data yyyy-MM-dd (opcional)" },
                ["installments"] = new { type = "integer", description = "Novo nº de parcelas (opcional)" },
                ["notes"] = new { type = "string", description = "Novas observações (opcional)" },
            },
            ["id"]),

        Tool("list_card_purchases",
            "Lista compras pendentes de um cartão específico.",
            new Dictionary<string, object>
            {
                ["credit_card_id"] = new { type = "string", description = "ID ou nome do cartão (ex: GUID ou 'Nubank')" },
            },
            ["credit_card_id"]),
    ];

    private static object Tool(string name, string description,
        Dictionary<string, object> properties, string[] required) =>
        new
        {
            type = "function",
            function = new
            {
                name,
                description,
                parameters = new
                {
                    type = "object",
                    properties,
                    required
                }
            }
        };

    // ─── Argument records for JSON deserialization ───
    private record CreateDebtArgs(
        string Description, decimal Amount,
        [property: JsonPropertyName("due_date")] string DueDate, string? Notes);

    private record CreateRecurringDebtArgs(
        string Description, decimal Amount,
        [property: JsonPropertyName("recurring_day")] int RecurringDay,
        int Months,
        [property: JsonPropertyName("start_month")] string? StartMonth,
        string? Notes);

    private record EditDebtArgs(
        string Id, string? Description, decimal? Amount,
        [property: JsonPropertyName("due_date")] string? DueDate, string? Notes);

    private record CreateReceivableArgs(
        string Description, decimal Amount,
        [property: JsonPropertyName("expected_date")] string ExpectedDate, string? Notes);

    private record CreateRecurringReceivableArgs(
        string Description, decimal Amount,
        [property: JsonPropertyName("recurring_day")] int RecurringDay,
        int Months, string? Notes);

    private record EditReceivableArgs(
        string Id, string? Description, decimal? Amount,
        [property: JsonPropertyName("expected_date")] string? ExpectedDate, string? Notes);

    private record CreateCreditCardArgs(
        string Name,
        [property: JsonPropertyName("last_four_digits")] string LastFourDigits,
        [property: JsonPropertyName("credit_limit")] decimal? CreditLimit,
        [property: JsonPropertyName("closing_day")] int ClosingDay,
        [property: JsonPropertyName("due_day")] int DueDay);

    private record EditCreditCardArgs(
        string Id, string? Name,
        [property: JsonPropertyName("credit_limit")] decimal? CreditLimit,
        [property: JsonPropertyName("closing_day")] int? ClosingDay,
        [property: JsonPropertyName("due_day")] int? DueDay,
        [property: JsonPropertyName("is_active")] bool? IsActive);

    private record CreateCardPurchaseArgs(
        [property: JsonPropertyName("credit_card_id")] string CreditCardId,
        string Description, decimal Amount,
        [property: JsonPropertyName("purchase_date")] string PurchaseDate,
        int Installments, string? Notes);

    private record EditCardPurchaseArgs(
        string Id, string? Description, decimal? Amount,
        [property: JsonPropertyName("purchase_date")] string? PurchaseDate,
        int? Installments, string? Notes);

    private record ListCardPurchasesArgs(
        [property: JsonPropertyName("credit_card_id")] string CreditCardId);
}

// Groq API response models
record GroqChatResponse(
    List<GroqChoice>? Choices);

record GroqChoice(
    GroqMessage? Message);

record GroqMessage(
    string? Role,
    string? Content,
    [property: JsonPropertyName("tool_calls")] List<GroqToolCall>? ToolCalls);

record GroqToolCall(
    string? Id,
    string? Type,
    GroqFunction? Function);

record GroqFunction(
    string? Name,
    string? Arguments);
