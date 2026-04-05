using System.Net.Http.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using InvoicesProjectEntities.Enums;

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
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
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

                return IsGeminiProvider(provider)
                    ? await CallGeminiProvider(userId, request, provider)
                    : await CallProvider(userId, request, provider);
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

    private static bool IsGeminiProvider(ProviderConfig provider) =>
        provider.Name.Equals("Gemini", StringComparison.OrdinalIgnoreCase);

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

        // If Groq rejects the LLM's tool call (e.g. number sent as string), retry without tools
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            if (errorBody.Contains("tool_use_failed") || errorBody.Contains("tool call validation failed"))
            {
                _logger.LogWarning("Tool call validation failed, retrying without tools: {Error}", errorBody);
                return await CallProviderWithoutTools(messages, client, provider);
            }
            response.EnsureSuccessStatusCode(); // throw for other errors
        }

        var result = await response.Content.ReadFromJsonAsync<GroqChatResponse>(JsonOptions);

        if (result?.Choices is null || result.Choices.Count == 0)
            return new ChatResponseDto("Não consegui processar sua mensagem. Tente novamente.", null);

        var choice = result.Choices[0];

        if (choice.Message?.ToolCalls is { Count: > 0 })        
            return await ExecuteToolCalls(userId, choice.Message.ToolCalls, messages, client, provider);
        
        return new ChatResponseDto(
            choice.Message?.Content ?? "Não entendi. Pode reformular?", null);
    }

    private async Task<ChatResponseDto> CallProviderWithoutTools(
        List<object> messages, HttpClient client, ProviderConfig provider)
    {
        var retryRequest = new
        {
            model = provider.Model,
            messages,
            temperature = 0.3,
            max_tokens = 1500,
        };

        var retryResponse = await client.PostAsJsonAsync(provider.BaseUrl, retryRequest, JsonOptions);
        retryResponse.EnsureSuccessStatusCode();

        var retryResult = await retryResponse.Content.ReadFromJsonAsync<GroqChatResponse>(JsonOptions);
        var reply = retryResult?.Choices?.FirstOrDefault()?.Message?.Content;

        return new ChatResponseDto(reply ?? "Não consegui processar sua mensagem. Tente novamente.", null);
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
        var fallbackDetails = new List<string>();
        string? planPayload = null;

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

            // Intercept purchase plan - don't send raw JSON to LLM
            planPayload ??= ExtractPlanPayload(toolResult);
            var contentForLlm = StripPlanMarkers(toolResult);

            if (!string.IsNullOrWhiteSpace(contentForLlm))
                fallbackDetails.Add(contentForLlm.Trim());

            toolMessages.Add(new { role = "tool", tool_call_id = toolCall.Id, content = contentForLlm });
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
            {
                if (planPayload is not null)
                    reply += $"\n\n<!--PURCHASE_PLAN-->{planPayload}<!--/PURCHASE_PLAN-->";
                return new ChatResponseDto(reply, actions);
            }
        }

        var summary = string.Join("\n", actions.Select(a =>
            a.Success ? $"✅ {a.Description}" : $"❌ {a.Description}"));

        var fallback = fallbackDetails.Count > 0
            ? string.Join("\n\n", fallbackDetails)
            : string.IsNullOrWhiteSpace(summary) ? "Ações processadas." : summary;
        if (planPayload is not null)
            fallback += $"\n\n<!--PURCHASE_PLAN-->{planPayload}<!--/PURCHASE_PLAN-->";

        return new ChatResponseDto(fallback, actions);
    }

    // ─── GEMINI PROVIDER ───

    private async Task<ChatResponseDto> CallGeminiProvider(Guid userId, ChatRequestDto request, ProviderConfig provider)
    {
        var (systemText, contents) = BuildGeminiContents(request, provider.DisableThinking);
        var toolDeclarations = GetGeminiToolDeclarations();

        var geminiRequest = new
        {
            systemInstruction = new { parts = new[] { new { text = systemText } } },
            contents,
            tools = new[] { new { functionDeclarations = toolDeclarations } },
            toolConfig = new { functionCallingConfig = new { mode = "AUTO" } },
            generationConfig = new { temperature = 0.1, maxOutputTokens = 1500 }
        };

        var client = _httpClientFactory.CreateClient("ChatProvider");
        client.DefaultRequestHeaders.Clear();

        var url = $"{provider.BaseUrl}/models/{provider.Model}:generateContent?key={provider.ApiKey}";
        var response = await client.PostAsJsonAsync(url, geminiRequest, JsonOptions);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(JsonOptions);

        if (result?.Candidates is not { Count: > 0 })
            return new ChatResponseDto("Não consegui processar sua mensagem. Tente novamente.", null);

        var parts = result.Candidates[0].Content?.Parts;

        if (parts is null || parts.Count == 0)
            return new ChatResponseDto("Não entendi. Pode reformular?", null);

        var functionCalls = parts.Where(p => p.FunctionCall is not null).ToList();
        if (functionCalls.Count > 0)
            return await ExecuteGeminiToolCalls(userId, functionCalls, contents, systemText, client, provider);

        var text = string.Join("", parts.Where(p => p.Text is not null).Select(p => p.Text));
        return new ChatResponseDto(
            string.IsNullOrWhiteSpace(text) ? "Não entendi. Pode reformular?" : text, null);
    }

    private async Task<ChatResponseDto> ExecuteGeminiToolCalls(
        Guid userId,
        List<GeminiPart> functionCallParts,
        List<object> contents,
        string systemText,
        HttpClient client,
        ProviderConfig provider)
    {
        var actions = new List<ChatActionResult>();
        var fallbackDetails = new List<string>();

        var modelParts = functionCallParts.Select(p => (object)new
        {
            functionCall = new { name = p.FunctionCall!.Name, args = p.FunctionCall.Args }
        }).ToList();

        var responseParts = new List<object>();
        foreach (var part in functionCallParts)
        {
            var fc = part.FunctionCall!;
            var argsJson = fc.Args?.ValueKind == JsonValueKind.Object ? fc.Args.Value.GetRawText() : "{}";

            _logger.LogInformation("Executando tool (Gemini): {Function} com args: {Args}", fc.Name, argsJson);

            string toolResult;
            try
            {
                toolResult = await ExecuteFunction(userId, fc.Name!, argsJson, actions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao executar tool {Function}", fc.Name);
                toolResult = $"Erro: {ex.Message}";
                actions.Add(new ChatActionResult(fc.Name!, ex.Message, false));
            }

            responseParts.Add(new
            {
                functionResponse = new
                {
                    name = fc.Name,
                    response = new { result = toolResult }
                }
            });
        }

        // Extract plan payload before sending to LLM
        string? planPayload = null;
        var cleanedResponseParts = new List<object>();
        foreach (var rp in responseParts)
        {
            var rpJson = JsonSerializer.Serialize(rp, JsonOptions);
            using var rpDoc = JsonDocument.Parse(rpJson);
            var resultStr = rpDoc.RootElement
                .GetProperty("function_response")
                .GetProperty("response")
                .GetProperty("result")
                .GetString() ?? "";

            planPayload ??= ExtractPlanPayload(resultStr);
            var cleanResult = StripPlanMarkers(resultStr);

            if (!string.IsNullOrWhiteSpace(cleanResult))
                fallbackDetails.Add(cleanResult.Trim());

            var frName = rpDoc.RootElement.GetProperty("function_response").GetProperty("name").GetString();
            cleanedResponseParts.Add(new
            {
                functionResponse = new
                {
                    name = frName,
                    response = new { result = cleanResult }
                }
            });
        }

        var followUpContents = new List<object>(contents);
        followUpContents.Add(new { role = "model", parts = modelParts });
        followUpContents.Add(new { role = "function", parts = cleanedResponseParts });

        var followUpRequest = new
        {
            systemInstruction = new { parts = new[] { new { text = systemText } } },
            contents = followUpContents,
            generationConfig = new { temperature = 0.3, maxOutputTokens = 1500 }
        };

        var url = $"{provider.BaseUrl}/models/{provider.Model}:generateContent?key={provider.ApiKey}";
        var followUpResponse = await client.PostAsJsonAsync(url, followUpRequest, JsonOptions);

        if (followUpResponse.IsSuccessStatusCode)
        {
            var followUpResult = await followUpResponse.Content
                .ReadFromJsonAsync<GeminiResponse>(JsonOptions);
            var replyText = followUpResult?.Candidates?.FirstOrDefault()?.Content?.Parts?
                .Where(p => p.Text is not null)
                .Select(p => p.Text)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(replyText))
            {
                if (planPayload is not null)
                    replyText += $"\n\n<!--PURCHASE_PLAN-->{planPayload}<!--/PURCHASE_PLAN-->";
                return new ChatResponseDto(replyText, actions);
            }
        }

        var summary = string.Join("\n", actions.Select(a =>
            a.Success ? $"✅ {a.Description}" : $"❌ {a.Description}"));
        var fallback = fallbackDetails.Count > 0
            ? string.Join("\n\n", fallbackDetails)
            : string.IsNullOrWhiteSpace(summary) ? "Ações processadas." : summary;
        if (planPayload is not null)
            fallback += $"\n\n<!--PURCHASE_PLAN-->{planPayload}<!--/PURCHASE_PLAN-->";
        return new ChatResponseDto(fallback, actions);
    }

    private const string PlanStart = "<!--PURCHASE_PLAN-->";
    private const string PlanEnd = "<!--/PURCHASE_PLAN-->";

    private static string? ExtractPlanPayload(string text)
    {
        var startIdx = text.IndexOf(PlanStart, StringComparison.Ordinal);
        var endIdx = text.IndexOf(PlanEnd, StringComparison.Ordinal);
        if (startIdx < 0 || endIdx < 0) return null;
        return text[(startIdx + PlanStart.Length)..endIdx];
    }

    private static string StripPlanMarkers(string text)
    {
        var startIdx = text.IndexOf(PlanStart, StringComparison.Ordinal);
        var endIdx = text.IndexOf(PlanEnd, StringComparison.Ordinal);
        if (startIdx < 0 || endIdx < 0) return text;
        return (text[..startIdx] + "Plano de compra gerado com sucesso. Apresente um breve resumo ao usuário." + text[(endIdx + PlanEnd.Length)..]).Trim();
    }

    private static DateOnly ParseDate(string dateStr) =>
        DateOnly.ParseExact(dateStr, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatCurrency(decimal value) =>
        $"R${value.ToString("F2", CultureInfo.InvariantCulture)}";

    private static readonly string[] MonthNames =
    ["", "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez"];

    private static readonly Dictionary<string, int> MonthLookup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jan"] = 1, ["janeiro"] = 1,
        ["fev"] = 2, ["fevereiro"] = 2,
        ["mar"] = 3, ["marco"] = 3, ["março"] = 3,
        ["abr"] = 4, ["abril"] = 4,
        ["mai"] = 5, ["maio"] = 5,
        ["jun"] = 6, ["junho"] = 6,
        ["jul"] = 7, ["julho"] = 7,
        ["ago"] = 8, ["agosto"] = 8,
        ["set"] = 9, ["setembro"] = 9,
        ["out"] = 10, ["outubro"] = 10,
        ["nov"] = 11, ["novembro"] = 11,
        ["dez"] = 12, ["dezembro"] = 12,
    };

    private static string? NormalizeMonthKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var input = raw.Trim().ToLowerInvariant();

        if (Regex.IsMatch(input, "^\\d{4}-\\d{2}$"))
            return input;

        var m1 = Regex.Match(input, "^(\\d{2})/(\\d{4})$");
        if (m1.Success)
            return $"{m1.Groups[2].Value}-{m1.Groups[1].Value}";

        var m2 = Regex.Match(input, "^([a-zç]{3,9})/(\\d{4})$");
        if (m2.Success && MonthLookup.TryGetValue(m2.Groups[1].Value, out var monthNum))
            return $"{m2.Groups[2].Value}-{monthNum:D2}";

        if (MonthLookup.TryGetValue(input, out var monthOnly))
            return $"{DateTime.UtcNow.Year}-{monthOnly:D2}";

        return null;
    }

    private static string NormalizeMonthReference(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "invoice";
        var input = raw.Trim().ToLowerInvariant();

        if (input is "fatura" or "invoice" or "cobranca" or "cobrança")
            return "invoice";
        if (input is "compra" or "purchase")
            return "purchase";
        if (input is "ambos" or "both")
            return "both";

        return "invoice";
    }

    // Mirrors exactly CardPurchaseRepository.IsBilledInMonth: single-installment purchases ALWAYS bill in purchase month
    private static string GetBillingMonthKey(DateOnly purchaseDate, int closingDay, int totalInstallments, int installmentNumber)
    {
        var firstBillingMonth = totalInstallments <= 1 || purchaseDate.Day <= closingDay
            ? new DateTime(purchaseDate.Year, purchaseDate.Month, 1)
            : new DateTime(purchaseDate.Year, purchaseDate.Month, 1).AddMonths(1);
        return firstBillingMonth.AddMonths(installmentNumber - 1).ToString("yyyy-MM");
    }

    private static bool PurchaseMatchesMonth(DateOnly purchaseDate, int closingDay, int currentInstallment, int totalInstallments, string monthKey)
    {
        var purchaseMonthKey = purchaseDate.ToString("yyyy-MM");
        if (purchaseMonthKey == monthKey) return true;

        for (var inst = Math.Max(1, currentInstallment); inst <= totalInstallments; inst++)
        {
            var billedKey = GetBillingMonthKey(purchaseDate, closingDay, totalInstallments, inst);
            if (billedKey == monthKey) return true;
        }

        return false;
    }

    private static bool PurchaseMatchesBillingMonth(DateOnly purchaseDate, int closingDay, int currentInstallment, int totalInstallments, string monthKey)
    {
        for (var inst = Math.Max(1, currentInstallment); inst <= totalInstallments; inst++)
        {
            var billedKey = GetBillingMonthKey(purchaseDate, closingDay, totalInstallments, inst);
            if (billedKey == monthKey) return true;
        }

        return false;
    }

    private static DateOnly BuildInvoiceDueDate(int year, int month, int dueDay)
    {
        var lastDay = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, Math.Min(Math.Max(1, dueDay), lastDay));
    }

    private async Task<string> BuildAllCardPurchasesReport(Guid userId, string? monthFilter, string? monthReference = null)
    {
        var cards = await _creditCardService.GetByUserIdAsync(userId);
        if (!cards.Any()) return "Nenhum cartão cadastrado.";

        var targetMonth = NormalizeMonthKey(monthFilter);
        var reference = NormalizeMonthReference(monthReference);
        var sb = new System.Text.StringBuilder();
        var monthlyTotals = new SortedDictionary<string, decimal>();
        decimal grandTotalRemaining = 0;

        foreach (var card in cards)
        {
            var purchases = await _cardPurchaseService.GetPendingByCreditCardIdAsync(card.Id);
            var pendingList = purchases.ToList();
            if (pendingList.Count == 0) continue;

            var cardLines = new List<string>();
            foreach (var p in pendingList.Take(40))
            {
                var remaining = p.Installments - p.CurrentInstallment + 1;
                var purchaseMonthKey = p.PurchaseDate.ToString("yyyy-MM");

                var installmentValue = p.Amount / p.Installments;
                var firstBillingMonth = p.Installments <= 1 || p.PurchaseDate.Day <= card.ClosingDay
                    ? new DateTime(p.PurchaseDate.Year, p.PurchaseDate.Month, 1)
                    : new DateTime(p.PurchaseDate.Year, p.PurchaseDate.Month, 1).AddMonths(1);

                for (int inst = p.CurrentInstallment; inst <= p.Installments; inst++)
                {
                    var billingMonth = firstBillingMonth.AddMonths(inst - 1);
                    var key = billingMonth.ToString("yyyy-MM");

                    monthlyTotals.TryAdd(key, 0);
                    monthlyTotals[key] += installmentValue;

                    var includeByBilling = targetMonth is null || key == targetMonth;
                    var include = targetMonth is null || (reference switch
                    {
                        "purchase" => purchaseMonthKey == targetMonth,
                        "both" => includeByBilling || purchaseMonthKey == targetMonth,
                        _ => includeByBilling,
                    });

                    if (include)
                    {
                        cardLines.Add($"  • {p.Description}: {FormatCurrency(installmentValue)}/parcela ({inst}/{p.Installments}x) • compra {p.PurchaseDate:dd/MM/yyyy} • fatura {MonthNames[billingMonth.Month]}/{billingMonth.Year}");
                    }
                }

                if (targetMonth is not null
                    && reference != "invoice"
                    && purchaseMonthKey == targetMonth
                    && !cardLines.Any(line => line.Contains($"{p.Description}:", StringComparison.Ordinal)))
                {
                    var firstBilling = firstBillingMonth;
                    cardLines.Add($"  • {p.Description}: {FormatCurrency(installmentValue)}/parcela ({p.CurrentInstallment}/{p.Installments}x) • compra {p.PurchaseDate:dd/MM/yyyy} • próxima fatura {MonthNames[firstBilling.Month]}/{firstBilling.Year}");
                }

                grandTotalRemaining += installmentValue * remaining;
            }

            if (cardLines.Count > 0)
            {
                sb.AppendLine($"📌 {card.Name} (final {card.LastFourDigits}, fecha dia {card.ClosingDay}, vence dia {card.DueDay}):");
                foreach (var line in cardLines) sb.AppendLine(line);
            }
        }

        if (sb.Length == 0)
        {
            if (targetMonth is not null)
            {
                var parts = targetMonth.Split('-');
                var monthNum = int.Parse(parts[1]);
                var scope = reference == "purchase"
                    ? "com compra realizada"
                    : reference == "both" ? "por compra ou fatura" : "em fatura";
                return $"Nenhuma compra pendente de cartão {scope} para {MonthNames[monthNum]}/{parts[0]}.";
            }
            return "Nenhuma compra pendente em nenhum cartão.";
        }

        if (monthlyTotals.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("📊 Projeção mensal de faturas de cartão (valor que cai na fatura de cada mês):");
            foreach (var (month, total) in monthlyTotals)
            {
                if (targetMonth is not null && month != targetMonth) continue;
                var parts = month.Split('-');
                var monthNum = int.Parse(parts[1]);
                sb.AppendLine($"  {MonthNames[monthNum]}/{parts[0]}: {FormatCurrency(total)}");
            }
            if (targetMonth is null)
                sb.AppendLine($"  Total restante (todas parcelas futuras): {FormatCurrency(grandTotalRemaining)}");
        }

        return sb.ToString().TrimEnd();
    }

    private async Task<string> BuildUpcomingDueItemsReport(Guid userId, int days)
    {
        var horizon = Math.Clamp(days, 1, 31);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var end = today.AddDays(horizon);

        var debts = (await _debtService.GetPendingByUserIdAsync(userId))
            .Where(d => d.DueDate >= today && d.DueDate <= end)
            .OrderBy(d => d.DueDate)
            .ToList();

        var receivables = (await _receivableService.GetPendingByUserIdAsync(userId))
            .Where(r => r.ExpectedDate >= today && r.ExpectedDate <= end)
            .OrderBy(r => r.ExpectedDate)
            .ToList();

        var cardInstallments = new List<(DateOnly DueDate, string CardName, string LastFour, string Description, decimal InstallmentValue, int Installment, int TotalInstallments)>();
        var cards = await _creditCardService.GetByUserIdAsync(userId);
        foreach (var card in cards)
        {
            var purchases = await _cardPurchaseService.GetPendingByCreditCardIdAsync(card.Id);
            foreach (var p in purchases)
            {
                for (var inst = p.CurrentInstallment; inst <= p.Installments; inst++)
                {
                    var billingKey = GetBillingMonthKey(p.PurchaseDate, card.ClosingDay, p.Installments, inst);
                    var split = billingKey.Split('-');
                    var dueDate = BuildInvoiceDueDate(int.Parse(split[0]), int.Parse(split[1]), card.DueDay);
                    if (dueDate < today || dueDate > end) continue;

                    cardInstallments.Add((
                        dueDate,
                        card.Name,
                        card.LastFourDigits,
                        p.Description,
                        p.Amount / p.Installments,
                        inst,
                        p.Installments));
                }
            }
        }

        cardInstallments = cardInstallments.OrderBy(c => c.DueDate).ThenBy(c => c.CardName).ToList();

        var outgoing = debts.Sum(d => d.Amount) + cardInstallments.Sum(c => c.InstallmentValue);
        var incoming = receivables.Sum(r => r.Amount);
        var net = incoming - outgoing;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Janela: hoje ({today:dd/MM/yyyy}) até {end:dd/MM/yyyy} ({horizon} dias)");
        sb.AppendLine($"Saídas previstas: {FormatCurrency(outgoing)} | Entradas previstas: {FormatCurrency(incoming)} | Saldo líquido: {FormatCurrency(net)}");
        sb.AppendLine();

        sb.AppendLine("📉 Débitos a vencer:");
        if (debts.Count == 0) sb.AppendLine("• Nenhum débito no período.");
        foreach (var d in debts.Take(20))
            sb.AppendLine($"• {d.Description} — {FormatCurrency(d.Amount)} (venc. {d.DueDate:dd/MM/yyyy})");

        sb.AppendLine();
        sb.AppendLine("💳 Faturas/parcelas de cartão no período:");
        if (cardInstallments.Count == 0) sb.AppendLine("• Nenhuma parcela com vencimento no período.");
        foreach (var c in cardInstallments.Take(25))
            sb.AppendLine($"• {c.CardName} final {c.LastFour} — {c.Description}: {FormatCurrency(c.InstallmentValue)} ({c.Installment}/{c.TotalInstallments}x) • vence {c.DueDate:dd/MM/yyyy}");

        sb.AppendLine();
        sb.AppendLine("📈 Recebíveis previstos:");
        if (receivables.Count == 0) sb.AppendLine("• Nenhum recebível no período.");
        foreach (var r in receivables.Take(20))
            sb.AppendLine($"• {r.Description} — {FormatCurrency(r.Amount)} (prev. {r.ExpectedDate:dd/MM/yyyy})");

        return sb.ToString().TrimEnd();
    }

    private async Task<string> BuildMonthlyDiagnostic(Guid userId, string? monthArg)
    {
        var targetMonth = NormalizeMonthKey(monthArg) ?? DateTime.UtcNow.ToString("yyyy-MM");
        var split = targetMonth.Split('-');
        var year = int.Parse(split[0]);
        var month = int.Parse(split[1]);

        // Use ALL items for the month (paid + pending) so the diagnostic is a true committed picture
        var allDebts = (await _debtService.GetByUserIdAsync(userId))
            .Where(d => d.DueDate.Year == year && d.DueDate.Month == month)
            .OrderBy(d => d.DueDate)
            .ToList();

        var allReceivables = (await _receivableService.GetByUserIdAsync(userId))
            .Where(r => r.ExpectedDate.Year == year && r.ExpectedDate.Month == month)
            .OrderBy(r => r.ExpectedDate)
            .ToList();

        var pendingDebts = allDebts.Where(d => !d.IsPaid).ToList();
        var pendingReceivables = allReceivables.Where(r => !r.IsReceived).ToList();

        // Cards: loop from inst=1 (not CurrentInstallment) to include already-paid installments
        decimal cardsTotal = 0;
        decimal cardsPending = 0;
        var cardHighlights = new List<string>();
        var cards = await _creditCardService.GetByUserIdAsync(userId);
        foreach (var card in cards)
        {
            var cardCommitted = 0m;
            var cardPendingAmt = 0m;
            var allPurchases = (await _cardPurchaseService.GetByCreditCardIdAsync(card.Id)).ToList();
            foreach (var p in allPurchases)
            {
                var installmentValue = p.Amount / p.Installments;
                for (var inst = 1; inst <= p.Installments; inst++)
                {
                    var billingKey = GetBillingMonthKey(p.PurchaseDate, card.ClosingDay, p.Installments, inst);
                    if (billingKey != targetMonth) continue;
                    cardCommitted += installmentValue;
                    // Pending: only installments that haven't been paid yet
                    if (!p.IsPaid && inst >= p.CurrentInstallment)
                        cardPendingAmt += installmentValue;
                }
            }

            if (cardCommitted > 0)
                cardHighlights.Add($"• {card.Name} (final {card.LastFourDigits}): {FormatCurrency(cardCommitted)} total • {FormatCurrency(cardPendingAmt)} pendente");

            cardsTotal += cardCommitted;
            cardsPending += cardPendingAmt;
        }

        var debtsTotal = allDebts.Sum(d => d.Amount);
        var receivablesTotal = allReceivables.Sum(r => r.Amount);
        var debtsPending = pendingDebts.Sum(d => d.Amount);
        var receivablesPending = pendingReceivables.Sum(r => r.Amount);

        var committedOutgoing = debtsTotal + cardsTotal;
        var committedBalance = receivablesTotal - committedOutgoing;
        var pendingOutgoing = debtsPending + cardsPending;
        var pendingBalance = receivablesPending - pendingOutgoing;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Diagnóstico {MonthNames[month]}/{year} (visão commitada — inclui itens já pagos)");
        sb.AppendLine();
        sb.AppendLine($"📊 Comprometido no mês:");
        sb.AppendLine($"• Recebíveis: {FormatCurrency(receivablesTotal)}");
        sb.AppendLine($"• Débitos: {FormatCurrency(debtsTotal)}");
        sb.AppendLine($"• Cartões (fatura do mês): {FormatCurrency(cardsTotal)}");
        sb.AppendLine($"• Saídas totais: {FormatCurrency(committedOutgoing)} | Saldo: {FormatCurrency(committedBalance)}");
        sb.AppendLine();
        sb.AppendLine($"⏳ Ainda pendente:");
        sb.AppendLine($"• A receber: {FormatCurrency(receivablesPending)} | A pagar: {FormatCurrency(pendingOutgoing)} | Saldo livre: {FormatCurrency(pendingBalance)}");
        sb.AppendLine();

        if (cardHighlights.Count > 0)
        {
            sb.AppendLine("💳 Cartões (comprometido | pendente):");
            foreach (var line in cardHighlights) sb.AppendLine(line);
            sb.AppendLine();
        }

        sb.AppendLine("📉 Débitos do mês:");
        if (allDebts.Count == 0) sb.AppendLine("• Nenhum débito neste mês.");
        foreach (var d in allDebts.Take(10))
            sb.AppendLine($"• {(d.IsPaid ? "✅" : "⏳")} {d.Description} — {FormatCurrency(d.Amount)} (venc. {d.DueDate:dd/MM/yyyy})");

        sb.AppendLine();
        sb.AppendLine("📈 Recebíveis do mês:");
        if (allReceivables.Count == 0) sb.AppendLine("• Nenhum recebível neste mês.");
        foreach (var r in allReceivables.Take(10))
            sb.AppendLine($"• {(r.IsReceived ? "✅" : "⏳")} {r.Description} — {FormatCurrency(r.Amount)} (prev. {r.ExpectedDate:dd/MM/yyyy})");

        return sb.ToString().TrimEnd();
    }

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
                var dto = new CreateDebtDto(args.Description, args.Amount, ParseDate(args.DueDate), args.Notes, args.Category);
                var debt = await _debtService.CreateAsync(userId, dto);
                actions.Add(new ChatActionResult("create_debt",
                    $"Débito '{debt.Description}' R${debt.Amount:F2} venc. {debt.DueDate:dd/MM/yyyy} [{debt.Category}]", true));
                return $"Débito criado: {debt.Description} • {FormatCurrency(debt.Amount)} • venc. {debt.DueDate:dd/MM/yyyy} • categoria {debt.Category}.";
            }

            case "create_recurring_debt":
            {
                var args = JsonSerializer.Deserialize<CreateRecurringDebtArgs>(argsJson, JsonOptions)!;
                var startDate = !string.IsNullOrEmpty(args.StartMonth)
                    ? ParseDate($"{args.StartMonth}-01")
                    : (DateOnly?)null;
                var dto = new CreateRecurringDebtDto(args.Description, args.Amount, args.RecurringDay, args.Months, startDate, args.Notes, args.Category);
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
                    args.Notes,
                    args.Category);
                var debt = await _debtService.UpdateAsync(ParseGuidSafe(args.Id, "débito"), dto);
                actions.Add(new ChatActionResult("edit_debt",
                    $"Débito '{debt.Description}' atualizado", true));
                return $"Débito atualizado: {debt.Description}, R${debt.Amount:F2}, venc. {debt.DueDate:dd/MM/yyyy}";
            }

            case "list_pending_debts":
            {
                var args = JsonSerializer.Deserialize<ListPendingDebtsArgs>(argsJson, JsonOptions);
                var monthFilter = NormalizeMonthKey(args?.Month);
                var debts = await _debtService.GetPendingByUserIdAsync(userId);
                var debtList = debts.ToList();
                if (!string.IsNullOrWhiteSpace(monthFilter))
                    debtList = debtList.Where(d => d.DueDate.ToString("yyyy-MM") == monthFilter).ToList();
                if (debtList.Count == 0)
                {
                    actions.Add(new ChatActionResult("list_pending_debts", "Listou débitos pendentes", true));
                    if (!string.IsNullOrWhiteSpace(monthFilter))
                    {
                        var parts = monthFilter.Split('-');
                        return $"Nenhum débito pendente para {MonthNames[int.Parse(parts[1])]}/{parts[0]}.";
                    }
                    return "Nenhum débito pendente.";
                }
                var grouped = debtList.GroupBy(d => d.DueDate.ToString("yyyy-MM")).OrderBy(g => g.Key);
                var sb = new System.Text.StringBuilder("Débitos pendentes por mês:\n\n");
                foreach (var g in grouped)
                {
                    var parts = g.Key.Split('-');
                    var monthTotal = g.Sum(d => d.Amount);
                    sb.AppendLine($"📅 {MonthNames[int.Parse(parts[1])]}/{parts[0]} (total: {FormatCurrency(monthTotal)})");
                    foreach (var d in g.Take(15))
                        sb.AppendLine($"• {d.Description} — {FormatCurrency(d.Amount)} (venc. {d.DueDate:dd/MM/yyyy})");
                    sb.AppendLine();
                }
                actions.Add(new ChatActionResult("list_pending_debts", "Listou débitos pendentes", true));
                return sb.ToString().TrimEnd();
            }

            // ─── RECEBÍVEIS ───
            case "create_receivable":
            {
                var args = JsonSerializer.Deserialize<CreateReceivableArgs>(argsJson, JsonOptions)!;
                var dto = new CreateReceivableDto(args.Description, args.Amount, ParseDate(args.ExpectedDate), args.Notes);
                var receivable = await _receivableService.CreateAsync(userId, dto);
                actions.Add(new ChatActionResult("create_receivable",
                    $"Recebível '{receivable.Description}' R${receivable.Amount:F2} previsão {receivable.ExpectedDate:dd/MM/yyyy}", true));
                return $"Recebível criado: {receivable.Description} • {FormatCurrency(receivable.Amount)} • previsão {receivable.ExpectedDate:dd/MM/yyyy}.";
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
                var args = JsonSerializer.Deserialize<ListPendingReceivablesArgs>(argsJson, JsonOptions);
                var monthFilter = NormalizeMonthKey(args?.Month);
                var receivables = await _receivableService.GetPendingByUserIdAsync(userId);
                var recList = receivables.ToList();
                if (!string.IsNullOrWhiteSpace(monthFilter))
                    recList = recList.Where(r => r.ExpectedDate.ToString("yyyy-MM") == monthFilter).ToList();
                if (recList.Count == 0)
                {
                    actions.Add(new ChatActionResult("list_pending_receivables", "Listou recebíveis pendentes", true));
                    if (!string.IsNullOrWhiteSpace(monthFilter))
                    {
                        var parts = monthFilter.Split('-');
                        return $"Nenhum recebível pendente para {MonthNames[int.Parse(parts[1])]}/{parts[0]}.";
                    }
                    return "Nenhum recebível pendente.";
                }
                var grouped = recList.GroupBy(r => r.ExpectedDate.ToString("yyyy-MM")).OrderBy(g => g.Key);
                var sb = new System.Text.StringBuilder("Recebíveis pendentes por mês:\n\n");
                foreach (var g in grouped)
                {
                    var parts = g.Key.Split('-');
                    var monthTotal = g.Sum(r => r.Amount);
                    sb.AppendLine($"📅 {MonthNames[int.Parse(parts[1])]}/{parts[0]} (total: {FormatCurrency(monthTotal)})");
                    foreach (var r in g.Take(15))
                        sb.AppendLine($"• {r.Description} — {FormatCurrency(r.Amount)} (previsão {r.ExpectedDate:dd/MM/yyyy}){(r.IsRecurring ? " • recorrente" : "")}");
                    sb.AppendLine();
                }
                actions.Add(new ChatActionResult("list_pending_receivables", "Listou recebíveis pendentes", true));
                return sb.ToString().TrimEnd();
            }

            // ─── CARTÕES DE CRÉDITO ───
            case "create_credit_card":
            {
                var args = JsonSerializer.Deserialize<CreateCreditCardArgs>(argsJson, JsonOptions)!;
                var dto = new CreateCreditCardDto(args.Name, args.LastFourDigits, args.CreditLimit, args.ClosingDay, args.DueDay);
                var card = await _creditCardService.CreateAsync(userId, dto);
                actions.Add(new ChatActionResult("create_credit_card",
                    $"Cartão '{card.Name}' final {card.LastFourDigits} criado", true));
                return $"Cartão criado: {card.Name} • final {card.LastFourDigits} • limite {FormatCurrency(card.CreditLimit ?? 0)} • fecha dia {card.ClosingDay} • vence dia {card.DueDay}.";
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
                    $"• {c.Name} (final {c.LastFourDigits}) — Limite: {FormatCurrency(c.CreditLimit ?? 0)} • Fecha dia {c.ClosingDay} • Vence dia {c.DueDay} • Pendente: {FormatCurrency(c.TotalPending)}");
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
                    ParseDate(args.PurchaseDate), args.Installments, args.Notes, args.Category);
                var purchase = await _cardPurchaseService.CreateAsync(dto);
                actions.Add(new ChatActionResult("create_card_purchase",
                    $"Compra '{purchase.Description}' R${purchase.Amount:F2} em {purchase.Installments}x registrada", true));
                return $"Compra registrada: {purchase.Description} • {FormatCurrency(purchase.Amount)} • {purchase.Installments}x.";
            }

            case "edit_card_purchase":
            {
                var args = JsonSerializer.Deserialize<EditCardPurchaseArgs>(argsJson, JsonOptions)!;
                var dto = new UpdateCardPurchaseDto(
                    args.Description,
                    args.Amount,
                    !string.IsNullOrEmpty(args.PurchaseDate) ? ParseDate(args.PurchaseDate) : null,
                    args.Installments,
                    null,
                    args.Notes,
                    args.Category);
                var purchase = await _cardPurchaseService.UpdateAsync(ParseGuidSafe(args.Id, "compra"), dto);
                actions.Add(new ChatActionResult("edit_card_purchase",
                    $"Compra '{purchase.Description}' atualizada", true));
                return $"Compra atualizada: {purchase.Description}, R${purchase.Amount:F2}";
            }

            case "list_card_purchases":
            {
                var args = JsonSerializer.Deserialize<ListCardPurchasesArgs>(argsJson, JsonOptions)!;
                var reference = NormalizeMonthReference(args.MonthReference);
                if (args.CreditCardId.Equals("todos", StringComparison.OrdinalIgnoreCase)
                    || args.CreditCardId.Equals("all", StringComparison.OrdinalIgnoreCase)
                    || args.CreditCardId.Contains("todos", StringComparison.OrdinalIgnoreCase))
                {
                    actions.Add(new ChatActionResult("list_card_purchases", "Listou compras de todos os cartões", true));
                    var allReport = await BuildAllCardPurchasesReport(userId, args.Month, reference);
                    return $"Compras pendentes em todos os cartões:\n{allReport}";
                }

                var cardId = await ResolveCreditCardIdAsync(userId, args.CreditCardId);
                var purchases = await _cardPurchaseService.GetPendingByCreditCardIdAsync(cardId);
                var monthFilter = NormalizeMonthKey(args.Month);
                var card = await _creditCardService.GetByIdAsync(cardId);
                if (card is null)
                    return "Cartão não encontrado.";
                var filteredPurchases = string.IsNullOrWhiteSpace(monthFilter)
                    ? purchases
                    : purchases.Where(p => reference switch
                    {
                        "purchase" => p.PurchaseDate.ToString("yyyy-MM") == monthFilter,
                        "both" => PurchaseMatchesMonth(
                            p.PurchaseDate,
                            card.ClosingDay,
                            p.CurrentInstallment,
                            p.Installments,
                            monthFilter),
                        _ => PurchaseMatchesBillingMonth(
                            p.PurchaseDate,
                            card.ClosingDay,
                            p.CurrentInstallment,
                            p.Installments,
                            monthFilter),
                    });

                var list = filteredPurchases.Take(15).Select(p =>
                    $"• {p.Description} — {FormatCurrency(p.Amount)} ({p.CurrentInstallment}/{p.Installments}x) • compra em {p.PurchaseDate:dd/MM/yyyy}");
                var result = list.Any()
                    ? $"Compras pendentes:\n{string.Join("\n", list)}"
                    : "Nenhuma compra pendente neste cartão.";
                actions.Add(new ChatActionResult("list_card_purchases", "Listou compras do cartão", true));
                return result;
            }

            case "list_all_card_purchases":
            {
                var args = JsonSerializer.Deserialize<ListAllCardPurchasesArgs>(argsJson, JsonOptions);
                var reference = NormalizeMonthReference(args?.MonthReference);
                var report = await BuildAllCardPurchasesReport(userId, args?.Month, reference);
                var result = $"Compras pendentes em todos os cartões:\n{report}";
                actions.Add(new ChatActionResult("list_all_card_purchases", "Listou compras de todos os cartões", true));
                return result;
            }

            case "get_upcoming_due_items":
            {
                var args = JsonSerializer.Deserialize<GetUpcomingDueItemsArgs>(argsJson, JsonOptions);
                var horizonDays = args?.Days ?? 7;
                var report = await BuildUpcomingDueItemsReport(userId, horizonDays);
                actions.Add(new ChatActionResult("get_upcoming_due_items", $"Consultou vencimentos dos próximos {Math.Clamp(horizonDays, 1, 31)} dias", true));
                return report;
            }

            case "get_monthly_diagnostic":
            {
                var args = JsonSerializer.Deserialize<GetMonthlyDiagnosticArgs>(argsJson, JsonOptions);
                var report = await BuildMonthlyDiagnostic(userId, args?.Month);
                actions.Add(new ChatActionResult("get_monthly_diagnostic", "Gerou diagnóstico mensal", true));
                return report;
            }

            default:
                return $"Função '{functionName}' não reconhecida.";

            // ─── PLANEJAMENTO DE COMPRA ───
            case "generate_purchase_plan":
            {
                var args = JsonSerializer.Deserialize<GeneratePurchasePlanArgs>(argsJson, JsonOptions)!;
                var plan = await BuildPurchasePlan(userId, args);
                var planJson = JsonSerializer.Serialize(plan, JsonOptions);
                actions.Add(new ChatActionResult("generate_purchase_plan", $"Plano de compra gerado: {args.ProductName}", true));
                return $"<!--PURCHASE_PLAN-->{planJson}<!--/PURCHASE_PLAN-->";
            }
        }
    }

    private static string GetSystemPrompt(bool disableThinking)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");
        var noThink = disableThinking ? "\n/no_think" : "";

        return $"""
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
            - Se o usuário pedir um mês específico (ex: "abril", "abr/2026", "2026-04"), inclua esse mês no parâmetro month da ferramenta de listagem.
            - Se o usuário pedir "todos os cartões", use list_all_card_purchases (NÃO use list_card_purchases com credit_card_id="todos").
                        - Para consultas de cartão com mês específico, use month_reference conforme intenção:
                            "fatura/debitos do cartão no mês" => month_reference="invoice";
                            "compras feitas no mês" => month_reference="purchase";
                            se estiver ambíguo, use month_reference="both" e explique os dois recortes.

            REGRA DE CONSULTA DE SALDO/LIMITE (OBRIGATÓRIA):
            - Quando o usuário perguntar se pode comprar algo, quanto tem de limite, saldo disponível ou qualquer valor calculado, você DEVE chamar list_credit_cards ou list_card_purchases ANTES de responder.
            - NUNCA faça cálculos de cabeça com valores do histórico da conversa. Os dados podem ter mudado.
            - O limite disponível é: Limite total - Pendente (campo "Pendente" retornado pela ferramenta). Use APENAS esse valor.

            REGRA DE VISÃO FINANCEIRA COMPLETA (OBRIGATÓRIA):
                        - Quando o usuário perguntar quanto está devendo, qual o total de dívidas, despesas do mês, ou pedir uma visão geral financeira, você DEVE chamar list_pending_debts, list_all_card_purchases e list_pending_receivables para dar uma resposta completa.
                        - list_all_card_purchases retorna no final uma "Projeção mensal de faturas de cartão" com o valor EXATO que cai na fatura de CADA MÊS. Use APENAS o valor do mês em questão, NÃO some o total geral de todas as parcelas futuras como se fosse gasto de um mês só.
                        - Débitos pendentes: filtre pelo vencimento do mês em questão. Compras no cartão: use o valor do mês da projeção. Recebíveis: filtre pela data prevista do mês.
                        - SEMPRE calcule saldo_livre do mês = recebiveis_do_mês - (debitos_do_mês + cartoes_do_mês).
                        - Exemplo: "Abril/2026: Recebíveis R$12.300 | Débitos R$1.691,74 | Cartões R$1.986,72 | Saídas R$3.678,46 | Saldo livre R$8.621,54"
                        - Para esse cenário, prefira usar a ferramenta get_monthly_diagnostic.

                        REGRA DE VENCIMENTOS PRÓXIMOS (OBRIGATÓRIA):
                        - Quando o usuário pedir "o que vence nos próximos X dias", "o que tenho essa semana", "quais contas vencem em breve", você DEVE chamar a ferramenta get_upcoming_due_items.
                        - Se o usuário não informar X, use 7 dias por padrão.
                        - SEMPRE liste TODOS os itens retornados pela ferramenta com nome completo, valor e data. NUNCA diga apenas "você tem 1 débito de R$X" sem dizer o nome do débito.
                        - Para get_monthly_diagnostic, apresente os dois blocos: comprometido total e pendente. Destaque o saldo livre do que ainda está pendente.

                        REGRA DE PLANEJAMENTO DE COMPRA E META (OBRIGATÓRIA):
                        - Quando o usuário pedir "melhor forma de comprar" algo, comparar à vista vs parcelado, simular compra, ou perguntar se consegue manter meta de economia, você DEVE chamar a ferramenta generate_purchase_plan.
                        - Extraia do pedido: nome do produto, preço total, desconto PIX (padrão 10%), meta de economia (se mencionada), mês de início.
                        - Se faltar o preço do produto, peça ao usuário. Os demais campos são opcionais.
                        - NÃO faça cálculos manuais. A ferramenta faz toda a projeção e gera a visualização automaticamente.
                        - Após chamar a ferramenta, dê um resumo breve e amigável da recomendação ao usuário. O detalhamento completo com tabelas e cenários será exibido visualmente pelo sistema.

            Regras gerais:
            - Se o usuário não informar o ano, assuma {DateTime.UtcNow.Year}.
            - Se não informar o mês, assuma o mês atual ({currentMonth}).
            - Valores devem ser números positivos.
            - Datas no formato yyyy-MM-dd para as funções.
            - IMPORTANTE: ao chamar ferramentas, valores numéricos (amount, total_price, credit_limit, etc.) DEVEM ser enviados como números JSON (ex: 5000), NUNCA como strings entre aspas (ex: "5000").
            - Responda sempre em português brasileiro, de forma concisa e amigável.
            - Use emojis moderadamente.
            - Se o usuário pedir algo fora de finanças, diga educadamente que só trata de finanças.
            - Para editar, o usuário pode referenciar itens pelo nome/descrição. Se ambíguo, liste os itens primeiro para ele escolher.
            - Para compras no cartão, se o usuário não especificar qual cartão, liste os cartões disponíveis e peça para ele escolher.

            REGRA DE CATEGORIZAÇÃO AUTOMÁTICA (OBRIGATÓRIA):
            - Ao criar débitos ou compras no cartão, você DEVE sempre enviar o campo "category" com a categoria mais adequada.
            - Categorias válidas: {string.Join(", ", ExpenseCategory.All)}.
            - Escolha a categoria com base na descrição do gasto. Exemplos: "Almoco" → Alimentação, "Uber" → Transporte, "Netflix" → Assinaturas, "Aluguel" → Moradia, "Feira" → Mercado, "Dentista" → Saúde, "Presente namorada" → Presentes, "Ajuda mãe" → Família.
            - Se não conseguir determinar, use "Outros".
            - NÃO pergunte a categoria ao usuário. Deduza automaticamente pela descrição.
            {noThink}
            """;
    }

    private static List<object> BuildMessages(ChatRequestDto request, bool disableThinking)
    {
        var messages = new List<object>
        {
            new { role = "system", content = GetSystemPrompt(disableThinking) }
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

    private static (string systemText, List<object> contents) BuildGeminiContents(
        ChatRequestDto request, bool disableThinking)
    {
        var systemText = GetSystemPrompt(disableThinking);
        var contents = new List<object>();

        if (request.History is { Count: > 0 })
        {
            foreach (var msg in request.History.TakeLast(30))
            {
                var role = msg.Role == "assistant" ? "model" : msg.Role;
                contents.Add(new { role, parts = new[] { new { text = msg.Content } } });
            }
        }

        contents.Add(new { role = "user", parts = new[] { new { text = request.Message } } });

        return (systemText, contents);
    }

    private static List<object> GetToolDefinitions() =>
    [
        // ─── DÉBITOS ───
        Tool("create_debt",
            "Cria um novo débito simples (conta a pagar única).",
            new Dictionary<string, object>
            {
                ["description"] = new { type = "string", description = "Descrição do débito" },
                ["amount"] = new { type = "number", description = "Valor em reais (número, ex: 5000)" },
                ["due_date"] = new { type = "string", description = "Data de vencimento yyyy-MM-dd" },
                ["notes"] = new { type = "string", description = "Observações opcionais" },
                ["category"] = new { type = "string", description = "Categoria do gasto (ex: Alimentação, Moradia, Transporte, Saúde, Lazer, Assinaturas, Mercado, Família, Outros)" },
            },
            ["description", "amount", "due_date", "category"]),

        Tool("create_recurring_debt",
            "Cria débitos recorrentes mensais (ex: ajuda familiar, plano mensal). Gera N meses automaticamente.",
            new Dictionary<string, object>
            {
                ["description"] = new { type = "string", description = "Descrição" },
                ["amount"] = new { type = "number", description = "Valor mensal em reais (número, ex: 1500)" },
                ["recurring_day"] = new { type = "integer", description = "Dia do mês (1-28)" },
                ["months"] = new { type = "integer", description = "Quantidade de meses (1-60)" },
                ["start_month"] = new { type = "string", description = "Mês de início yyyy-MM (opcional, padrão mês atual)" },
                ["notes"] = new { type = "string", description = "Observações opcionais" },
                ["category"] = new { type = "string", description = "Categoria do gasto" },
            },
            ["description", "amount", "recurring_day", "months", "category"]),

        Tool("edit_debt",
            "Edita um débito existente. Precisa do ID do débito. Só envie os campos que devem mudar.",
            new Dictionary<string, object>
            {
                ["id"] = new { type = "string", description = "ID (GUID) do débito" },
                ["description"] = new { type = "string", description = "Nova descrição (opcional)" },
                ["amount"] = new { type = "number", description = "Novo valor (opcional)" },
                ["due_date"] = new { type = "string", description = "Nova data yyyy-MM-dd (opcional)" },
                ["notes"] = new { type = "string", description = "Novas observações (opcional)" },
                ["category"] = new { type = "string", description = "Nova categoria (opcional)" },
            },
            ["id"]),

        Tool("list_pending_debts",
            "Lista débitos pendentes (não pagos) com seus IDs.",
            new Dictionary<string, object>
            {
                ["month"] = new { type = "string", description = "Mês opcional (yyyy-MM, MM/yyyy ou Abr/2026)" },
            }, []),

        // ─── RECEBÍVEIS ───
        Tool("create_receivable",
            "Cria um recebível simples (valor a receber único).",
            new Dictionary<string, object>
            {
                ["description"] = new { type = "string", description = "Descrição do recebível" },
                ["amount"] = new { type = "number", description = "Valor em reais (número, ex: 3000)" },
                ["expected_date"] = new { type = "string", description = "Data prevista yyyy-MM-dd" },
                ["notes"] = new { type = "string", description = "Observações opcionais" },
            },
            ["description", "amount", "expected_date"]),

        Tool("create_recurring_receivable",
            "Cria recebíveis recorrentes mensais (ex: salário). Gera N meses automaticamente.",
            new Dictionary<string, object>
            {
                ["description"] = new { type = "string", description = "Descrição" },
                ["amount"] = new { type = "number", description = "Valor mensal em reais (número, ex: 8000)" },
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
            new Dictionary<string, object>
            {
                ["month"] = new { type = "string", description = "Mês opcional (yyyy-MM, MM/yyyy ou Abr/2026)" },
            }, []),

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
                ["amount"] = new { type = "number", description = "Valor total em reais (número, ex: 2500)" },
                ["purchase_date"] = new { type = "string", description = "Data da compra yyyy-MM-dd" },
                ["installments"] = new { type = "integer", description = "Número de parcelas (1 = à vista)" },
                ["notes"] = new { type = "string", description = "Observações opcionais" },
                ["category"] = new { type = "string", description = "Categoria da compra" },
            },
            ["credit_card_id", "description", "amount", "purchase_date", "installments", "category"]),

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
                ["category"] = new { type = "string", description = "Nova categoria (opcional)" },
            },
            ["id"]),

        Tool("list_card_purchases",
            "Lista compras pendentes de um cartão específico.",
            new Dictionary<string, object>
            {
                ["credit_card_id"] = new { type = "string", description = "ID ou nome do cartão (ex: GUID ou 'Nubank')" },
                ["month"] = new { type = "string", description = "Mês opcional (yyyy-MM, MM/yyyy ou Abr/2026)" },
                ["month_reference"] = new { type = "string", description = "Referência do mês: invoice (mês da fatura), purchase (mês da compra) ou both (ambos). Padrão: invoice" },
            },
            ["credit_card_id"]),

        Tool("list_all_card_purchases",
            "Lista TODAS as compras pendentes de TODOS os cartões do usuário. Use quando precisar de uma visão geral das compras no cartão.",
            new Dictionary<string, object>
            {
                ["month"] = new { type = "string", description = "Mês opcional (yyyy-MM, MM/yyyy ou Abr/2026)" },
                ["month_reference"] = new { type = "string", description = "Referência do mês: invoice (mês da fatura), purchase (mês da compra) ou both (ambos). Padrão: invoice" },
            }, []),

        Tool("get_upcoming_due_items",
            "Mostra vencimentos e recebimentos dos próximos dias (débitos, parcelas/faturas de cartão e recebíveis), com saldo líquido do período.",
            new Dictionary<string, object>
            {
                ["days"] = new { type = "integer", description = "Janela em dias (1-31). Padrão: 7" },
            }, []),

        Tool("get_monthly_diagnostic",
            "Gera diagnóstico financeiro completo de um mês: recebíveis, débitos, cartão por mês de fatura, saídas totais e saldo livre.",
            new Dictionary<string, object>
            {
                ["month"] = new { type = "string", description = "Mês opcional (yyyy-MM, MM/yyyy ou Abr/2026). Padrão: mês atual" },
            }, []),

        Tool("generate_purchase_plan",
            "Gera um plano de compra estruturado com projeção mensal, cenários (PIX, cartão à vista, parcelado) e recomendação. Use SEMPRE que o usuário quiser comprar algo e pedir para planejar/simular/comparar formas de pagamento. O resultado é exibido visualmente no frontend.",
            new Dictionary<string, object>
            {
                ["product_name"] = new { type = "string", description = "Nome do produto (ex: '2x Monitores')" },
                ["total_price"] = new { type = "number", description = "Preço total em reais (número, ex: 5000)" },
                ["pix_discount_percent"] = new { type = "number", description = "Percentual de desconto no PIX (número, padrão 10)" },
                ["savings_goal"] = new { type = "number", description = "Meta de economia mensal do usuário (número, 0 se não informado)" },
                ["start_month"] = new { type = "string", description = "Mês de início yyyy-MM (padrão: próximo mês)" },
            },
            ["product_name", "total_price"]),
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

    private static List<JsonElement> GetGeminiToolDeclarations()
    {
        var openAiTools = GetToolDefinitions();
        var json = JsonSerializer.Serialize(openAiTools, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray()
            .Select(t => t.GetProperty("function").Clone())
            .ToList();
    }

    // ─── Argument records for JSON deserialization ───
    private record CreateDebtArgs(
        string Description, decimal Amount,
        [property: JsonPropertyName("due_date")] string DueDate, string? Notes,
        string? Category);

    private record CreateRecurringDebtArgs(
        string Description, decimal Amount,
        [property: JsonPropertyName("recurring_day")] int RecurringDay,
        int Months,
        [property: JsonPropertyName("start_month")] string? StartMonth,
        string? Notes, string? Category);

    private record EditDebtArgs(
        string Id, string? Description, decimal? Amount,
        [property: JsonPropertyName("due_date")] string? DueDate, string? Notes,
        string? Category);

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
        int Installments, string? Notes, string? Category);

    private record EditCardPurchaseArgs(
        string Id, string? Description, decimal? Amount,
        [property: JsonPropertyName("purchase_date")] string? PurchaseDate,
        int? Installments, string? Notes, string? Category);

    private record ListCardPurchasesArgs(
        [property: JsonPropertyName("credit_card_id")] string CreditCardId,
        [property: JsonPropertyName("month")] string? Month,
        [property: JsonPropertyName("month_reference")] string? MonthReference);

    private record ListAllCardPurchasesArgs(
        [property: JsonPropertyName("month")] string? Month,
        [property: JsonPropertyName("month_reference")] string? MonthReference);

    private record ListPendingDebtsArgs(
        [property: JsonPropertyName("month")] string? Month);

    private record ListPendingReceivablesArgs(
        [property: JsonPropertyName("month")] string? Month);

    private record GetUpcomingDueItemsArgs(
        [property: JsonPropertyName("days")] int? Days);

    private record GetMonthlyDiagnosticArgs(
        [property: JsonPropertyName("month")] string? Month);

    private record GeneratePurchasePlanArgs(
        [property: JsonPropertyName("product_name")] string ProductName,
        [property: JsonPropertyName("total_price")] decimal TotalPrice,
        [property: JsonPropertyName("pix_discount_percent")] decimal? PixDiscountPercent,
        [property: JsonPropertyName("savings_goal")] decimal? SavingsGoal,
        [property: JsonPropertyName("start_month")] string? StartMonth);

    // ─── Purchase Plan Builder ───

    private async Task<PurchasePlanResult> BuildPurchasePlan(Guid userId, GeneratePurchasePlanArgs args)
    {
        var startMonth = !string.IsNullOrEmpty(args.StartMonth)
            ? DateOnly.ParseExact(args.StartMonth + "-01", "yyyy-MM-dd")
            : DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(1);

        var pixDiscount = args.PixDiscountPercent ?? 10m;
        var pixPrice = args.TotalPrice * (1 - pixDiscount / 100m);
        var savingsGoal = args.SavingsGoal ?? 0m;

        // Gather all data
        var debts = (await _debtService.GetPendingByUserIdAsync(userId)).ToList();
        var receivables = (await _receivableService.GetPendingByUserIdAsync(userId)).ToList();
        var cards = (await _creditCardService.GetByUserIdAsync(userId)).ToList();

        // Build card billing by month
        var cardMonthly = new SortedDictionary<string, decimal>();
        var cardMonthlyByCard = new Dictionary<Guid, SortedDictionary<string, decimal>>();
        foreach (var card in cards)
        {
            if (!cardMonthlyByCard.TryGetValue(card.Id, out var perCardMonthly))
            {
                perCardMonthly = new SortedDictionary<string, decimal>();
                cardMonthlyByCard[card.Id] = perCardMonthly;
            }

            var purchases = (await _cardPurchaseService.GetPendingByCreditCardIdAsync(card.Id)).ToList();
            foreach (var p in purchases)
            {
                var installmentAmount = p.Installments > 0 ? p.Amount / p.Installments : p.Amount;
                var firstBilling = p.Installments <= 1 || p.PurchaseDate.Day <= card.ClosingDay
                    ? new DateTime(p.PurchaseDate.Year, p.PurchaseDate.Month, 1)
                    : new DateTime(p.PurchaseDate.Year, p.PurchaseDate.Month, 1).AddMonths(1);
                for (int inst = p.CurrentInstallment; inst <= p.Installments; inst++)
                {
                    var key = firstBilling.AddMonths(inst - 1).ToString("yyyy-MM");
                    cardMonthly.TryAdd(key, 0);
                    cardMonthly[key] += installmentAmount;

                    perCardMonthly.TryAdd(key, 0);
                    perCardMonthly[key] += installmentAmount;
                }
            }
        }

        // Build monthly projection (12 months)
        var projection = new List<MonthProjection>();
        string[] monthNames = ["", "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez"];

        for (int i = 0; i < 12; i++)
        {
            var month = startMonth.AddMonths(i);
            var key = month.ToString("yyyy-MM");
            var label = $"{monthNames[month.Month]}/{month.Year}";

            var monthReceivables = receivables
                .Where(r => r.ExpectedDate.ToString("yyyy-MM") == key)
                .Sum(r => r.Amount);

            var monthDebts = debts
                .Where(d => d.DueDate.ToString("yyyy-MM") == key)
                .Sum(d => d.Amount);

            cardMonthly.TryGetValue(key, out var monthCards);

            var totalExpenses = monthDebts + monthCards;
            var freeBalance = monthReceivables - totalExpenses;
            var afterSavings = freeBalance - savingsGoal;

            projection.Add(new MonthProjection(key, label, monthReceivables, monthDebts,
                monthCards, totalExpenses, freeBalance, afterSavings));
        }

        // ─── Card Strategy: analyze best cards for this purchase ───
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeCards = cards.Where(c => c.IsActive).ToList();
        var cardAnalysis = new List<CardPlanAnalysis>();

        foreach (var card in activeCards)
        {
            var currentAvailable = card.AvailableLimit ?? 0m;
            if (card.CreditLimit == null) continue; // skip no-limit cards for strategy

            var startKey = startMonth.ToString("yyyy-MM");
            var restoredUntilStart = 0m;
            if (cardMonthlyByCard.TryGetValue(card.Id, out var perCardMonthly))
            {
                restoredUntilStart = perCardMonthly
                    .Where(kvp => string.CompareOrdinal(kvp.Key, startKey) < 0)
                    .Sum(kvp => kvp.Value);
            }

            var projectedAvailableAtStart = Math.Min(card.CreditLimit.Value, currentAvailable + restoredUntilStart);

            // Calculate payment timeline (same logic as GetBestCardForTodayAsync)
            DateOnly nextClosing;
            if (today.Day <= card.ClosingDay)
                nextClosing = new DateOnly(today.Year, today.Month, Math.Min(card.ClosingDay, DateTime.DaysInMonth(today.Year, today.Month)));
            else
            {
                var nextMonth = today.AddMonths(1);
                nextClosing = new DateOnly(nextMonth.Year, nextMonth.Month, Math.Min(card.ClosingDay, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month)));
            }

            DateOnly dueDate;
            if (card.DueDay > card.ClosingDay)
                dueDate = new DateOnly(nextClosing.Year, nextClosing.Month, Math.Min(card.DueDay, DateTime.DaysInMonth(nextClosing.Year, nextClosing.Month)));
            else
            {
                var dueMonth = nextClosing.AddMonths(1);
                dueDate = new DateOnly(dueMonth.Year, dueMonth.Month, Math.Min(card.DueDay, DateTime.DaysInMonth(dueMonth.Year, dueMonth.Month)));
            }

            var daysUntilPayment = dueDate.DayNumber - today.DayNumber;
            var afterClosing = today.Day > card.ClosingDay;

            cardAnalysis.Add(new CardPlanAnalysis(
                card.Id, card.Name, card.LastFourDigits,
                card.CreditLimit.Value, projectedAvailableAtStart,
                currentAvailable, restoredUntilStart,
                card.ClosingDay, card.DueDay,
                daysUntilPayment, afterClosing,
                nextClosing, dueDate));
        }

        // Sort: longest payment window first
        cardAnalysis = cardAnalysis.OrderByDescending(c => c.DaysUntilPayment).ToList();

        // Build card strategy
        var cardStrategies = new List<CardPlanStrategy>();

        // Strategy 1: Single card (if any card has enough limit)
        foreach (var card in cardAnalysis.Where(c => c.AvailableLimit >= args.TotalPrice))
        {
            var explanation = card.AfterClosing
                ? $"Fatura já fechou (dia {card.ClosingDay}). Compra entra na próxima fatura, vence {card.DueDate:dd/MM/yyyy} — {card.DaysUntilPayment} dias para pagar."
                : $"Fatura ainda aberta (fecha dia {card.ClosingDay}). Vence {card.DueDate:dd/MM/yyyy} — {card.DaysUntilPayment} dias para pagar.";

            cardStrategies.Add(new CardPlanStrategy(
                "single",
                $"Tudo no {card.CardName}",
                [new CardAllocation(card.CardId, card.CardName, card.LastFourDigits, args.TotalPrice, card.AvailableLimit, card.DaysUntilPayment, card.DueDate, explanation)],
                card.DaysUntilPayment,
                true));
        }

        // Strategy 2: Split across multiple cards (if no single card covers it)
        if (!cardAnalysis.Any(c => c.AvailableLimit >= args.TotalPrice) && cardAnalysis.Count >= 2)
        {
            var remaining = args.TotalPrice;
            var allocations = new List<CardAllocation>();
            var minDays = int.MaxValue;

            foreach (var card in cardAnalysis)
            {
                if (remaining <= 0) break;
                if (card.AvailableLimit <= 0) continue;

                var useAmount = Math.Min(remaining, card.AvailableLimit);
                remaining -= useAmount;
                minDays = Math.Min(minDays, card.DaysUntilPayment);

                var explanation = card.AfterClosing
                    ? $"Pós-fechamento (dia {card.ClosingDay}). Vence {card.DueDate:dd/MM/yyyy} — {card.DaysUntilPayment}d."
                    : $"Pré-fechamento (dia {card.ClosingDay}). Vence {card.DueDate:dd/MM/yyyy} — {card.DaysUntilPayment}d.";

                allocations.Add(new CardAllocation(
                    card.CardId, card.CardName, card.LastFourDigits,
                    useAmount, card.AvailableLimit, card.DaysUntilPayment, card.DueDate, explanation));
            }

            var covered = remaining <= 0;
            var stratLabel = covered
                ? $"Dividir entre {allocations.Count} cartões"
                : $"Dividir entre {allocations.Count} cartões (faltam {remaining:F2})";

            cardStrategies.Add(new CardPlanStrategy(
                "split", stratLabel, allocations,
                minDays == int.MaxValue ? 0 : minDays, covered));
        }

        // Total available across all cards
        var totalAvailable = cardAnalysis.Sum(c => c.AvailableLimit);
        var bestCardForToday = cardAnalysis.FirstOrDefault();

        // Build per-card bill projections (existing committed installments per month)
        var billsByCard = new List<CardBillProjection>();
        foreach (var card in cardAnalysis)
        {
            var months = new List<CardBillMonth>();
            foreach (var proj in projection)
            {
                decimal amount = 0;
                if (cardMonthlyByCard.TryGetValue(card.CardId, out var perCardMonthly))
                    perCardMonthly.TryGetValue(proj.Month, out amount);
                months.Add(new CardBillMonth(proj.Month, proj.Label, amount));
            }
            if (months.Any(m => m.Amount > 0))
                billsByCard.Add(new CardBillProjection(card.CardId, card.CardName, card.LastFourDigits, months));
        }

        var cardStrategyResult = new CardStrategyResult(
            totalAvailable,
            totalAvailable >= args.TotalPrice,
            bestCardForToday?.CardName,
            bestCardForToday?.DaysUntilPayment ?? 0,
            cardAnalysis.Select(c => new CardSummary(
                c.CardId, c.CardName, c.LastFourDigits,
                c.CreditLimit, c.AvailableLimit,
                c.CurrentAvailableLimit, c.RestoredByStartMonth,
                c.ClosingDay, c.DueDay,
                c.DaysUntilPayment, c.AfterClosing)).ToList(),
            cardStrategies,
            billsByCard);

        // Build scenarios
        var scenarios = new List<PurchaseScenario>();
        int[] installmentOptions = [1, 2, 3, 6, 10, 12];

        // PIX scenario
        var pixImpact = BuildScenarioImpact(projection, pixPrice, 1, savingsGoal);
        scenarios.Add(new PurchaseScenario("pix", $"PIX à vista ({pixDiscount:F0}% desc.)",
            pixPrice, 1, pixPrice, pixImpact.Viable, pixImpact.Months));

        // Card scenarios
        foreach (var n in installmentOptions)
        {
            var installmentValue = Math.Round(args.TotalPrice / n, 2);
            var impact = BuildScenarioImpact(projection, installmentValue, n, savingsGoal);
            var label = n == 1 ? "Cartão à vista" : $"Cartão {n}x sem juros";
            scenarios.Add(new PurchaseScenario("card", label,
                args.TotalPrice, n, installmentValue, impact.Viable, impact.Months));
        }

        // Find best scenario
        var bestScenario = scenarios
            .Where(s => s.Viable)
            .OrderByDescending(s => s.Type == "pix" ? 1 : 0)
            .ThenBy(s => s.TotalCost)
            .ThenByDescending(s => s.MonthlyImpact.Min(m => m.RemainingAfterSavings))
            .FirstOrDefault();

        // Build recommendation including card info
        var recommendation = bestScenario != null
            ? $"{bestScenario.Label} é a melhor opção. " +
              (bestScenario.Type == "pix"
                  ? $"Você economiza R${args.TotalPrice - pixPrice:F2} com o desconto. "
                  : "") +
              $"Parcela de R${bestScenario.InstallmentValue:F2}, mantendo a meta de R${savingsGoal:F2}/mês."
            : "Nenhum cenário é viável sem comprometer a meta. Considere adiar ou reduzir o valor.";

        if (bestScenario == null && !cardStrategyResult.CoversFullAmount)
        {
            var missing = Math.Max(0m, args.TotalPrice - cardStrategyResult.TotalAvailable);
            recommendation += $" Seus cartões atuais cobrem R${cardStrategyResult.TotalAvailable:F2}; faltam R${missing:F2}. " +
                "Se usar cartão externo/complementar, a projeção já considera o impacto da nova parcela somado às suas faturas atuais.";
        }

        if (bestScenario?.Type == "card" && bestCardForToday != null)
        {
            var cardTip = bestCardForToday.AfterClosing
                ? $" Use o {bestCardForToday.CardName} (•••• {bestCardForToday.LastFourDigits}) — fatura já fechou, próximo pagamento só em {bestCardForToday.DaysUntilPayment} dias."
                : $" Use o {bestCardForToday.CardName} (•••• {bestCardForToday.LastFourDigits}) — {bestCardForToday.DaysUntilPayment} dias até o vencimento.";
            recommendation += cardTip;
        }

        return new PurchasePlanResult(args.ProductName, args.TotalPrice, pixDiscount,
            pixPrice, savingsGoal, startMonth.ToString("yyyy-MM"),
            projection, scenarios, recommendation, cardStrategyResult);
    }

    private static (bool Viable, List<ScenarioMonthImpact> Months) BuildScenarioImpact(
        List<MonthProjection> projection, decimal installmentValue, int installments, decimal savingsGoal)
    {
        var months = new List<ScenarioMonthImpact>();
        bool viable = true;

        for (int i = 0; i < Math.Min(installments, projection.Count); i++)
        {
            var mp = projection[i];
            var totalCardBill = mp.Cards + installmentValue;
            var remaining = mp.AfterSavings - installmentValue;
            if (remaining < 0) viable = false;
            months.Add(new ScenarioMonthImpact(
                mp.Month,
                mp.Label,
                installmentValue,
                mp.Cards,
                totalCardBill,
                mp.Debts + totalCardBill,
                remaining));
        }

        return (viable, months);
    }

    // Plan result records
    private record PurchasePlanResult(
        [property: JsonPropertyName("product")] string Product,
        [property: JsonPropertyName("totalPrice")] decimal TotalPrice,
        [property: JsonPropertyName("pixDiscountPercent")] decimal PixDiscountPercent,
        [property: JsonPropertyName("pixPrice")] decimal PixPrice,
        [property: JsonPropertyName("savingsGoal")] decimal SavingsGoal,
        [property: JsonPropertyName("startMonth")] string StartMonth,
        [property: JsonPropertyName("monthlyProjection")] List<MonthProjection> MonthlyProjection,
        [property: JsonPropertyName("scenarios")] List<PurchaseScenario> Scenarios,
        [property: JsonPropertyName("recommendation")] string Recommendation,
        [property: JsonPropertyName("cardStrategy")] CardStrategyResult? CardStrategy);

    private record MonthProjection(
        [property: JsonPropertyName("month")] string Month,
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("receivables")] decimal Receivables,
        [property: JsonPropertyName("debts")] decimal Debts,
        [property: JsonPropertyName("cards")] decimal Cards,
        [property: JsonPropertyName("totalExpenses")] decimal TotalExpenses,
        [property: JsonPropertyName("freeBalance")] decimal FreeBalance,
        [property: JsonPropertyName("afterSavings")] decimal AfterSavings);

    private record PurchaseScenario(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("totalCost")] decimal TotalCost,
        [property: JsonPropertyName("installments")] int Installments,
        [property: JsonPropertyName("installmentValue")] decimal InstallmentValue,
        [property: JsonPropertyName("viable")] bool Viable,
        [property: JsonPropertyName("monthlyImpact")] List<ScenarioMonthImpact> MonthlyImpact);

    private record ScenarioMonthImpact(
        [property: JsonPropertyName("month")] string Month,
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("payment")] decimal Payment,
        [property: JsonPropertyName("baseCardBill")] decimal BaseCardBill,
        [property: JsonPropertyName("totalCardBill")] decimal TotalCardBill,
        [property: JsonPropertyName("totalOutflow")] decimal TotalOutflow,
        [property: JsonPropertyName("remainingAfterSavings")] decimal RemainingAfterSavings);

    // Card strategy records
    private record CardPlanAnalysis(
        Guid CardId, string CardName, string LastFourDigits,
        decimal CreditLimit, decimal AvailableLimit,
        decimal CurrentAvailableLimit, decimal RestoredByStartMonth,
        int ClosingDay, int DueDay,
        int DaysUntilPayment, bool AfterClosing,
        DateOnly NextClosing, DateOnly DueDate);

    private record CardStrategyResult(
        [property: JsonPropertyName("totalAvailable")] decimal TotalAvailable,
        [property: JsonPropertyName("coversFullAmount")] bool CoversFullAmount,
        [property: JsonPropertyName("bestCardName")] string? BestCardName,
        [property: JsonPropertyName("bestCardDaysUntilPayment")] int BestCardDaysUntilPayment,
        [property: JsonPropertyName("cards")] List<CardSummary> Cards,
        [property: JsonPropertyName("strategies")] List<CardPlanStrategy> Strategies,
        [property: JsonPropertyName("billsByCard")] List<CardBillProjection> BillsByCard);

    private record CardSummary(
        [property: JsonPropertyName("cardId")] Guid CardId,
        [property: JsonPropertyName("cardName")] string CardName,
        [property: JsonPropertyName("lastFourDigits")] string LastFourDigits,
        [property: JsonPropertyName("creditLimit")] decimal CreditLimit,
        [property: JsonPropertyName("availableLimit")] decimal AvailableLimit,
        [property: JsonPropertyName("currentAvailableLimit")] decimal CurrentAvailableLimit,
        [property: JsonPropertyName("restoredByStartMonth")] decimal RestoredByStartMonth,
        [property: JsonPropertyName("closingDay")] int ClosingDay,
        [property: JsonPropertyName("dueDay")] int DueDay,
        [property: JsonPropertyName("daysUntilPayment")] int DaysUntilPayment,
        [property: JsonPropertyName("afterClosing")] bool AfterClosing);

    private record CardPlanStrategy(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("allocations")] List<CardAllocation> Allocations,
        [property: JsonPropertyName("maxDaysUntilPayment")] int MaxDaysUntilPayment,
        [property: JsonPropertyName("coversFullAmount")] bool CoversFullAmount);

    private record CardAllocation(
        [property: JsonPropertyName("cardId")] Guid CardId,
        [property: JsonPropertyName("cardName")] string CardName,
        [property: JsonPropertyName("lastFourDigits")] string LastFourDigits,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("availableLimit")] decimal AvailableLimit,
        [property: JsonPropertyName("daysUntilPayment")] int DaysUntilPayment,
        [property: JsonPropertyName("dueDate")] DateOnly DueDate,
        [property: JsonPropertyName("explanation")] string Explanation);

    private record CardBillMonth(
        [property: JsonPropertyName("month")] string Month,
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("amount")] decimal Amount);

    private record CardBillProjection(
        [property: JsonPropertyName("cardId")] Guid CardId,
        [property: JsonPropertyName("cardName")] string CardName,
        [property: JsonPropertyName("lastFourDigits")] string LastFourDigits,
        [property: JsonPropertyName("months")] List<CardBillMonth> Months);
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

// Gemini API response models
record GeminiResponse(
    List<GeminiCandidate>? Candidates);

record GeminiCandidate(
    GeminiContent? Content);

record GeminiContent(
    string? Role,
    List<GeminiPart>? Parts);

record GeminiPart(
    string? Text,
    GeminiFunctionCall? FunctionCall);

record GeminiFunctionCall(
    string? Name,
    JsonElement? Args);
