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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<ChatService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public ChatService(
        IDebtService debtService,
        IReceivableService receivableService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ChatService> logger)
    {
        _debtService = debtService;
        _receivableService = receivableService;
        _httpClientFactory = httpClientFactory;
        _apiKey = configuration["Groq:ApiKey"]
            ?? throw new InvalidOperationException("Groq:ApiKey não configurada.");
        _model = configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";
        _logger = logger;
    }

    public async Task<ChatResponseDto> ProcessMessageAsync(Guid userId, ChatRequestDto request)
    {
        var messages = BuildMessages(request);
        var tools = GetToolDefinitions();

        var groqRequest = new
        {
            model = _model,
            messages,
            tools,
            tool_choice = "auto",
            temperature = 0.1,
            max_tokens = 1024,
        };

        var client = _httpClientFactory.CreateClient("Groq");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await client.PostAsJsonAsync(
            "https://api.groq.com/openai/v1/chat/completions",
            groqRequest, JsonOptions);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GroqChatResponse>(JsonOptions);

        if (result?.Choices is null || result.Choices.Count == 0)
            return new ChatResponseDto("Não consegui processar sua mensagem. Tente novamente.", null);

        var choice = result.Choices[0];

        if (choice.Message?.ToolCalls is { Count: > 0 })
        {
            return await ExecuteToolCalls(userId, choice.Message.ToolCalls, messages, client);
        }

        return new ChatResponseDto(
            choice.Message?.Content ?? "Não entendi. Pode reformular?", null);
    }

    private async Task<ChatResponseDto> ExecuteToolCalls(
        Guid userId,
        List<GroqToolCall> toolCalls,
        List<object> messages,
        HttpClient client)
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

        // Send tool results back to get a natural language summary
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
            model = _model,
            messages = followUp,
            temperature = 0.3,
            max_tokens = 512,
        };

        var followUpResponse = await client.PostAsJsonAsync(
            "https://api.groq.com/openai/v1/chat/completions",
            followUpRequest, JsonOptions);

        if (followUpResponse.IsSuccessStatusCode)
        {
            var followUpResult = await followUpResponse.Content
                .ReadFromJsonAsync<GroqChatResponse>(JsonOptions);
            var reply = followUpResult?.Choices?.FirstOrDefault()?.Message?.Content;
            if (!string.IsNullOrWhiteSpace(reply))
                return new ChatResponseDto(reply, actions);
        }

        // Fallback: build summary from actions
        var summary = string.Join("\n", actions.Select(a =>
            a.Success ? $"✅ {a.Description}" : $"❌ {a.Description}"));
        return new ChatResponseDto(
            string.IsNullOrWhiteSpace(summary) ? "Ações processadas." : summary, actions);
    }

    private async Task<string> ExecuteFunction(
        Guid userId, string functionName, string argsJson, List<ChatActionResult> actions)
    {
        switch (functionName)
        {
            case "create_debt":
            {
                var args = JsonSerializer.Deserialize<CreateDebtArgs>(argsJson, JsonOptions)!;
                var dto = new CreateDebtDto(
                    args.Description,
                    args.Amount,
                    DateTime.SpecifyKind(DateTime.Parse(args.DueDate), DateTimeKind.Utc),
                    args.Notes);
                var debt = await _debtService.CreateAsync(userId, dto);
                actions.Add(new ChatActionResult("create_debt",
                    $"Débito '{debt.Description}' de R${debt.Amount:F2} criado, venc. {debt.DueDate:dd/MM/yyyy}", true));
                return $"Débito criado com sucesso. ID: {debt.Id}, Descrição: {debt.Description}, Valor: {debt.Amount:F2}, Vencimento: {debt.DueDate:dd/MM/yyyy}";
            }

            case "create_receivable":
            {
                var args = JsonSerializer.Deserialize<CreateReceivableArgs>(argsJson, JsonOptions)!;
                var dto = new CreateReceivableDto(
                    args.Description,
                    args.Amount,
                    DateTime.SpecifyKind(DateTime.Parse(args.ExpectedDate), DateTimeKind.Utc),
                    args.Notes);
                var receivable = await _receivableService.CreateAsync(userId, dto);
                actions.Add(new ChatActionResult("create_receivable",
                    $"Recebível '{receivable.Description}' de R${receivable.Amount:F2} criado, previsão {receivable.ExpectedDate:dd/MM/yyyy}", true));
                return $"Recebível criado com sucesso. ID: {receivable.Id}, Descrição: {receivable.Description}, Valor: {receivable.Amount:F2}, Data: {receivable.ExpectedDate:dd/MM/yyyy}";
            }

            case "list_pending_debts":
            {
                var debts = await _debtService.GetPendingByUserIdAsync(userId);
                var list = debts.Take(10).Select(d =>
                    $"- {d.Description}: R${d.Amount:F2} (venc. {d.DueDate:dd/MM/yyyy})");
                var result = list.Any()
                    ? $"Débitos pendentes:\n{string.Join("\n", list)}"
                    : "Nenhum débito pendente.";
                actions.Add(new ChatActionResult("list_pending_debts", "Listou débitos pendentes", true));
                return result;
            }

            case "list_pending_receivables":
            {
                var receivables = await _receivableService.GetPendingByUserIdAsync(userId);
                var list = receivables.Take(10).Select(r =>
                    $"- {r.Description}: R${r.Amount:F2} (previsão {r.ExpectedDate:dd/MM/yyyy})");
                var result = list.Any()
                    ? $"Recebíveis pendentes:\n{string.Join("\n", list)}"
                    : "Nenhum recebível pendente.";
                actions.Add(new ChatActionResult("list_pending_receivables", "Listou recebíveis pendentes", true));
                return result;
            }

            default:
                return $"Função '{functionName}' não reconhecida.";
        }
    }

    private static List<object> BuildMessages(ChatRequestDto request)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");

        var messages = new List<object>
        {
            new
            {
                role = "system",
                content = $"""
                    Você é o Kash, assistente financeiro. Ajude o usuário a gerenciar débitos e recebíveis.
                    Hoje é {today}. Use as ferramentas disponíveis para criar débitos, recebíveis e listar pendências.
                    
                    Regras:
                    - Se o usuário não informar o ano, assuma {DateTime.UtcNow.Year}.
                    - Se não informar o mês, assuma o mês atual ({currentMonth}).
                    - Valores devem ser números positivos.
                    - Datas no formato yyyy-MM-dd para as funções.
                    - Responda sempre em português brasileiro, de forma concisa e amigável.
                    - Use emojis moderadamente para deixar a conversa agradável.
                    - Se o usuário pedir algo que não seja relacionado a finanças, responda educadamente que você só lida com finanças.
                    """
            }
        };

        if (request.History is { Count: > 0 })
        {
            foreach (var msg in request.History.TakeLast(20))
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }
        }

        messages.Add(new { role = "user", content = request.Message });

        return messages;
    }

    private static List<object> GetToolDefinitions() =>
    [
        new
        {
            type = "function",
            function = new
            {
                name = "create_debt",
                description = "Cria um novo débito (conta a pagar). Use quando o usuário quer registrar uma despesa, conta ou dívida.",
                parameters = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["description"] = new { type = "string", description = "Descrição do débito (ex: Conta de luz, Aluguel)" },
                        ["amount"] = new { type = "number", description = "Valor em reais (ex: 150.00)" },
                        ["due_date"] = new { type = "string", description = "Data de vencimento no formato yyyy-MM-dd" },
                        ["notes"] = new { type = "string", description = "Observações opcionais" },
                    },
                    required = new[] { "description", "amount", "due_date" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "create_receivable",
                description = "Cria um novo recebível (valor a receber). Use quando o usuário quer registrar um recebimento esperado, salário, pagamento de cliente, etc.",
                parameters = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["description"] = new { type = "string", description = "Descrição do recebível (ex: Salário, Freelance)" },
                        ["amount"] = new { type = "number", description = "Valor em reais (ex: 3000.00)" },
                        ["expected_date"] = new { type = "string", description = "Data esperada de recebimento no formato yyyy-MM-dd" },
                        ["notes"] = new { type = "string", description = "Observações opcionais" },
                    },
                    required = new[] { "description", "amount", "expected_date" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "list_pending_debts",
                description = "Lista os débitos pendentes (não pagos) do usuário.",
                parameters = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>(),
                    required = Array.Empty<string>()
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "list_pending_receivables",
                description = "Lista os recebíveis pendentes (não recebidos) do usuário.",
                parameters = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>(),
                    required = Array.Empty<string>()
                }
            }
        }
    ];

    // Internal DTOs for parsing Groq function arguments
    private record CreateDebtArgs(
        string Description,
        decimal Amount,
        [property: JsonPropertyName("due_date")] string DueDate,
        string? Notes);

    private record CreateReceivableArgs(
        string Description,
        decimal Amount,
        [property: JsonPropertyName("expected_date")] string ExpectedDate,
        string? Notes);
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
