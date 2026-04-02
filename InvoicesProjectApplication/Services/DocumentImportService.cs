using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InvoicesProjectApplication.Services;

public class DocumentImportService : IDocumentImportService
{
    private readonly IDebtService _debtService;
    private readonly ICardPurchaseService _cardPurchaseService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DocumentImportService> _logger;
    private readonly string _provider;
    private readonly string _ocrSpaceApiKey;
    private readonly string _ocrSpaceBaseUrl;
    private readonly string _ocrSpaceLanguage;
    private readonly string _geminiApiKey;
    private readonly string _geminiBaseUrl;
    private readonly string _geminiModel;
    private readonly List<LlmProviderConfig> _llmProviders;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private record LlmProviderConfig(
        string Name,
        string ApiKey,
        string Model,
        string BaseUrl,
        int? NumCtx);

    public DocumentImportService(
        IDebtService debtService,
        ICardPurchaseService cardPurchaseService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<DocumentImportService> logger)
    {
        _debtService = debtService;
        _cardPurchaseService = cardPurchaseService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        var ocrSpaceSection = configuration.GetSection("DocumentImport:OcrSpace");
        _ocrSpaceApiKey = ocrSpaceSection["ApiKey"] ?? "";
        _ocrSpaceBaseUrl = ocrSpaceSection["BaseUrl"] ?? "https://api.ocr.space/parse/image";
        _ocrSpaceLanguage = ocrSpaceSection["Language"] ?? "por";

        var configuredProvider = configuration["DocumentImport:Provider"];
        _provider = string.IsNullOrWhiteSpace(configuredProvider)
            ? (string.IsNullOrWhiteSpace(_ocrSpaceApiKey) ? "Gemini" : "OcrSpace")
            : configuredProvider.Trim();

        // Gemini Vision pode ser usado como OCR + extração estruturada
        var geminiSection = configuration.GetSection("ChatProvider:Providers:Gemini");
        _geminiApiKey = geminiSection["ApiKey"] ?? "";
        _geminiBaseUrl = geminiSection["BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta";
        _geminiModel = geminiSection["Model"] ?? "gemini-2.0-flash";

        _llmProviders = BuildLlmProviderChain(configuration);

        _logger.LogInformation("Document import provider ativo: {Provider}", _provider);
    }

    public async Task<DocumentExtractionResultDto> ExtractFromFileAsync(
        Stream fileStream, string fileName, string contentType)
    {
        _logger.LogInformation("Extraindo dados do documento: {FileName} ({ContentType})", fileName, contentType);

        byte[] fileBytes;
        using (var ms = new MemoryStream())
        {
            await fileStream.CopyToAsync(ms);
            fileBytes = ms.ToArray();
        }

        if (fileBytes.Length > 10 * 1024 * 1024)
            throw new ArgumentException("Arquivo muito grande. Máximo permitido: 10MB.");

        var extraction = _provider.Equals("OcrSpace", StringComparison.OrdinalIgnoreCase)
            ? await ExtractUsingOcrSpaceWithFallbackAsync(fileBytes, fileName, contentType)
            : await ExtractUsingGeminiWithFallbackAsync(fileBytes, fileName, contentType);

        var items = (extraction.Items ?? []).Select(i => new ExtractedItemDto(
            i.Description ?? "Item sem descrição",
            i.Amount,
            i.Date ?? DateTime.UtcNow.ToString("yyyy-MM-dd"),
            ExpenseCategory.Normalize(i.Category),
            i.Type == "card_purchase" ? "card_purchase" : "debt",
            i.Installments > 0 ? i.Installments : 1
        )).ToList();

        _logger.LogInformation("Extraídos {Count} itens do documento {FileName}", items.Count, fileName);

        return new DocumentExtractionResultDto(
            fileName,
            extraction.DocumentType ?? "unknown",
            items,
            extraction.Summary
        );
    }

    private async Task<ExtractionResult> ExtractUsingOcrSpaceWithFallbackAsync(
        byte[] fileBytes,
        string fileName,
        string contentType)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_ocrSpaceApiKey))
                throw new InvalidOperationException("OCR.Space ApiKey não configurada.");

            var ocrText = await ExtractTextWithOcrSpaceAsync(fileBytes, fileName, contentType);
            return await ParseTextWithLlmProvidersAsync(ocrText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha no OCR.Space. Tentando fallback com Gemini Vision.");

            if (HasGeminiConfigured())
                return await ExtractWithGeminiVisionAsync(fileBytes, fileName, contentType);

            throw;
        }
    }

    private async Task<ExtractionResult> ExtractUsingGeminiWithFallbackAsync(
        byte[] fileBytes,
        string fileName,
        string contentType)
    {
        try
        {
            if (!HasGeminiConfigured())
                throw new InvalidOperationException("Gemini ApiKey não configurada para importação de documentos.");

            return await ExtractWithGeminiVisionAsync(fileBytes, fileName, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha no Gemini Vision. Tentando fallback com OCR.Space.");

            if (string.IsNullOrWhiteSpace(_ocrSpaceApiKey))
                throw;

            var ocrText = await ExtractTextWithOcrSpaceAsync(fileBytes, fileName, contentType);
            return await ParseTextWithLlmProvidersAsync(ocrText);
        }
    }

    private bool HasGeminiConfigured() => !string.IsNullOrWhiteSpace(_geminiApiKey);

    private async Task<ExtractionResult> ExtractWithGeminiVisionAsync(
        byte[] fileBytes,
        string fileName,
        string contentType)
    {
        var base64Data = Convert.ToBase64String(fileBytes);
        var mimeType = GetMimeType(contentType, fileName);

        var prompt = $$"""
            Analise esta imagem/documento financeiro e extraia TODOS os itens de gasto encontrados.
            
            Regras:
            - Se for uma fatura de cartão de crédito, extraia cada compra individual.
            - Se for uma nota fiscal, extraia cada item.
            - Se for um boleto, extraia o valor e descrição.
            - Categorize cada item usando APENAS estas categorias: {{string.Join(", ", ExpenseCategory.All)}}.
            - Para cada item, determine se é "debt" (boleto, conta avulsa) ou "card_purchase" (compra de cartão).
            - Datas no formato yyyy-MM-dd.
            - Valores em reais, apenas números (sem R$).
            - Se houver parcelas, informe o número total de parcelas. Se for à vista, use 1.
            
            Responda EXCLUSIVAMENTE com JSON válido neste formato (sem markdown, sem ```json):
            {
              "document_type": "credit_card_statement|invoice|receipt|bill",
              "summary": "breve descrição do documento",
              "items": [
                {
                  "description": "Descrição do item",
                  "amount": 99.90,
                  "date": "2026-04-15",
                  "category": "Categoria",
                  "type": "debt|card_purchase",
                  "installments": 1
                }
              ]
            }
            
            Se não encontrar itens financeiros, retorne: {"document_type":"unknown","summary":"Documento não contém dados financeiros identificáveis","items":[]}
            """;

        var geminiRequest = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { inlineData = new { mimeType, data = base64Data } },
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new { temperature = 0.1, maxOutputTokens = 4096 }
        };

        var client = _httpClientFactory.CreateClient("ChatProvider");
        var url = $"{_geminiBaseUrl}/models/{_geminiModel}:generateContent?key={_geminiApiKey}";

        var response = await client.PostAsJsonAsync(url, geminiRequest, JsonOptions);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeminiVisionResponse>(JsonOptions);
        var textContent = result?.Candidates?.FirstOrDefault()?.Content?.Parts?
            .FirstOrDefault(p => p.Text is not null)?.Text;

        if (string.IsNullOrWhiteSpace(textContent))
            throw new InvalidOperationException("Não foi possível extrair dados do documento.");

        return ParseExtractionJson(textContent);
    }

    private async Task<string> ExtractTextWithOcrSpaceAsync(byte[] fileBytes, string fileName, string contentType)
    {
        var mimeType = GetMimeType(contentType, fileName);
        var base64Data = Convert.ToBase64String(fileBytes);
        var dataUri = $"data:{mimeType};base64,{base64Data}";

        using var multipart = new MultipartFormDataContent
        {
            { new StringContent(_ocrSpaceApiKey), "apikey" },
            { new StringContent(_ocrSpaceLanguage), "language" },
            { new StringContent("false"), "isOverlayRequired" },
            { new StringContent("2"), "OCREngine" },
            { new StringContent(dataUri), "base64Image" },
        };

        var client = _httpClientFactory.CreateClient("ChatProvider");
        var response = await client.PostAsync(_ocrSpaceBaseUrl, multipart);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OcrSpaceResponse>(JsonOptions);
        if (result is null)
            throw new InvalidOperationException("Resposta vazia do OCR.Space.");

        if (result.IsErroredOnProcessing)
        {
            var err = result.ErrorMessage?.FirstOrDefault() ?? "Erro desconhecido no OCR.Space.";
            throw new InvalidOperationException($"OCR.Space retornou erro: {err}");
        }

        var text = string.Join("\n", result.ParsedResults?
            .Where(p => !string.IsNullOrWhiteSpace(p.ParsedText))
            .Select(p => p.ParsedText!) ?? []);

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("OCR.Space não retornou texto legível para o documento.");

        return text;
    }

    private async Task<ExtractionResult> ParseTextWithLlmProvidersAsync(string ocrText)
    {
        if (_llmProviders.Count == 0 && !HasGeminiConfigured())
            throw new InvalidOperationException("Nenhum provedor de IA configurado para estruturar o texto do OCR.");

        var trimmedText = ocrText.Length > 18000 ? ocrText[..18000] : ocrText;

        var prompt = $$"""
            Você receberá texto bruto extraído por OCR de documentos financeiros.
            Interprete o conteúdo e extraia itens financeiros.

            Regras:
            - Se for fatura de cartão, extraia cada compra individual.
            - Se for nota fiscal, extraia cada item relevante.
            - Se for boleto/conta, extraia o título e valor principal.
            - Categorize cada item usando APENAS estas categorias: {{string.Join(", ", ExpenseCategory.All)}}.
            - Para cada item, use type = "debt" (conta avulsa) ou "card_purchase" (compra de cartão).
            - Datas no formato yyyy-MM-dd.
            - Valores em reais, apenas números (sem R$).
            - Atenção: OCR pode remover vírgula decimal (ex.: 236,21 virar 23621). Corrija isso usando o contexto do próprio texto OCR.
            - Se houver parcelas, informe o total de parcelas; à vista = 1.
            - Se não houver informação suficiente, use type debt e categoria Outros.

            Texto OCR:
            {{trimmedText}}

            Responda EXCLUSIVAMENTE com JSON válido neste formato (sem markdown, sem ```json):
            {
              "document_type": "credit_card_statement|invoice|receipt|bill|unknown",
              "summary": "breve descrição do documento",
              "items": [
                {
                  "description": "Descrição do item",
                  "amount": 99.90,
                  "date": "2026-04-15",
                  "category": "Categoria",
                  "type": "debt|card_purchase",
                  "installments": 1
                }
              ]
            }

            Se não encontrar itens financeiros, retorne:
            {"document_type":"unknown","summary":"Documento não contém dados financeiros identificáveis","items":[]}
            """;

        for (var i = 0; i < _llmProviders.Count; i++)
        {
            var provider = _llmProviders[i];
            var isLast = i == _llmProviders.Count - 1;

            try
            {
                var content = await CallOpenAiCompatibleProviderAsync(provider, prompt);
                var extraction = ParseExtractionJson(content);
                return NormalizeAmountsWithOcrText(extraction, trimmedText);
            }
            catch (HttpRequestException ex) when (!isLast && IsRateLimitOrUnavailable(ex))
            {
                _logger.LogWarning("Provider {Provider} indisponível ao estruturar OCR ({Status}), tentando fallback...",
                    provider.Name, ex.StatusCode);
            }
            catch (Exception ex) when (!isLast)
            {
                _logger.LogWarning(ex, "Falha no provider {Provider} ao estruturar OCR, tentando fallback...",
                    provider.Name);
            }
        }

        if (HasGeminiConfigured())
        {
            var content = await CallGeminiTextProviderAsync(prompt);
            var extraction = ParseExtractionJson(content);
            return NormalizeAmountsWithOcrText(extraction, trimmedText);
        }

        throw new InvalidOperationException("Não foi possível estruturar os dados extraídos pelo OCR.");
    }

    private async Task<string> CallOpenAiCompatibleProviderAsync(LlmProviderConfig provider, string prompt)
    {
        var request = new
        {
            model = provider.Model,
            messages = new[]
            {
                new { role = "system", content = "Você é um extrator de dados financeiros em JSON." },
                new { role = "user", content = prompt },
            },
            temperature = 0.1,
            max_tokens = 1800,
            options = provider.NumCtx.HasValue ? new { num_ctx = provider.NumCtx.Value } : (object?)null,
        };

        var client = _httpClientFactory.CreateClient("ChatProvider");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", provider.ApiKey);

        var response = await client.PostAsJsonAsync(provider.BaseUrl, request, JsonOptions);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GenericChatResponse>(JsonOptions);
        var content = result?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Resposta vazia do provedor de IA ao estruturar OCR.");

        return content;
    }

    private async Task<string> CallGeminiTextProviderAsync(string prompt)
    {
        var request = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new { temperature = 0.1, maxOutputTokens = 4096 }
        };

        var client = _httpClientFactory.CreateClient("ChatProvider");
        var url = $"{_geminiBaseUrl}/models/{_geminiModel}:generateContent?key={_geminiApiKey}";

        var response = await client.PostAsJsonAsync(url, request, JsonOptions);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeminiVisionResponse>(JsonOptions);
        var content = result?.Candidates?.FirstOrDefault()?.Content?.Parts?
            .FirstOrDefault(p => p.Text is not null)?.Text;

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Resposta vazia do Gemini ao estruturar OCR.");

        return content;
    }

    private static bool IsRateLimitOrUnavailable(HttpRequestException ex) =>
        ex.StatusCode is System.Net.HttpStatusCode.TooManyRequests
            or System.Net.HttpStatusCode.ServiceUnavailable
            or System.Net.HttpStatusCode.BadGateway
            or System.Net.HttpStatusCode.GatewayTimeout;

    private static List<LlmProviderConfig> BuildLlmProviderChain(IConfiguration configuration)
    {
        var active = configuration["ChatProvider:Active"] ?? "Groq";
        var fallbacks = configuration.GetSection("ChatProvider:Fallbacks").Get<string[]>() ?? [];
        var chain = new[] { active }.Concat(fallbacks);

        var providers = new List<LlmProviderConfig>();
        foreach (var name in chain)
        {
            if (name.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
                continue;

            var section = configuration.GetSection($"ChatProvider:Providers:{name}");
            var apiKey = section["ApiKey"];
            var baseUrl = section["BaseUrl"];
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(baseUrl))
                continue;

            providers.Add(new LlmProviderConfig(
                name,
                apiKey,
                section["Model"] ?? "llama-3.3-70b-versatile",
                baseUrl,
                int.TryParse(section["NumCtx"], out var ctx) ? ctx : null
            ));
        }

        return providers;
    }

    private static ExtractionResult ParseExtractionJson(string textContent)
    {
        // Limpa possível markdown wrapping
        textContent = textContent.Trim();
        if (textContent.StartsWith("```json")) textContent = textContent[7..];
        if (textContent.StartsWith("```")) textContent = textContent[3..];
        if (textContent.EndsWith("```")) textContent = textContent[..^3];
        textContent = textContent.Trim();

        var extraction = JsonSerializer.Deserialize<ExtractionResult>(textContent, JsonOptions);

        if (extraction is null)
            throw new InvalidOperationException("Resposta da IA não pôde ser interpretada.");

        return extraction;
    }

    private ExtractionResult NormalizeAmountsWithOcrText(ExtractionResult extraction, string ocrText)
    {
        if (extraction.Items is null || extraction.Items.Count == 0)
            return extraction;

        var candidates = ExtractMoneyCandidates(ocrText);
        if (candidates.Count == 0)
            return extraction;

        var normalizedItems = new List<ExtractionItem>(extraction.Items.Count);

        foreach (var item in extraction.Items)
        {
            var normalized = item;
            var originalAmount = item.Amount;

            if (originalAmount > 0 && !ContainsApprox(candidates, originalAmount))
            {
                var corrected = TryScaleCorrection(originalAmount, candidates);
                if (corrected.HasValue)
                {
                    normalized = item with { Amount = corrected.Value };
                    _logger.LogInformation(
                        "Valor corrigido por reconciliação OCR: {Original} -> {Corrected} para item {Description}",
                        originalAmount,
                        corrected.Value,
                        item.Description);
                }
            }

            normalizedItems.Add(normalized);
        }

        return extraction with { Items = normalizedItems };
    }

    private static decimal? TryScaleCorrection(decimal amount, List<decimal> candidates)
    {
        // Corrige casos clássicos de OCR: 236,21 -> 23621 e 100,00 -> 10000
        foreach (var divisor in new[] { 10m, 100m, 1000m })
        {
            var scaled = Math.Round(amount / divisor, 2);
            if (ContainsApprox(candidates, scaled))
                return scaled;
        }

        return null;
    }

    private static bool ContainsApprox(List<decimal> values, decimal value)
    {
        return values.Any(v => Math.Abs(v - value) <= 0.01m);
    }

    private static List<decimal> ExtractMoneyCandidates(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return [];

        var candidates = new HashSet<decimal>();

        // Captura padrões numéricos comuns em OCR: 236,21 | 1.234,56 | 3650 | 3,650
        const string pattern = @"(?<!\d)(\d{1,3}(?:[\.,]\d{3})*(?:[\.,]\d{2,3})|\d+[\.,]\d{2,3}|\d{1,8})(?!\d)";
        var matches = Regex.Matches(source, pattern);

        foreach (Match match in matches)
        {
            var token = match.Value;
            if (TryParseOcrMoneyToken(token, out var parsed))
                candidates.Add(parsed);
        }

        return candidates.ToList();
    }

    private static bool TryParseOcrMoneyToken(string token, out decimal value)
    {
        value = 0;
        token = token.Trim();

        if (token.Length == 0)
            return false;

        var commaCount = token.Count(c => c == ',');
        var dotCount = token.Count(c => c == '.');

        string normalized;

        if (commaCount > 0 && dotCount > 0)
        {
            // Usa o último separador como decimal (comportamento robusto para OCR)
            var lastComma = token.LastIndexOf(',');
            var lastDot = token.LastIndexOf('.');
            var decimalSeparator = lastComma > lastDot ? ',' : '.';

            normalized = decimalSeparator == ','
                ? token.Replace(".", "").Replace(',', '.')
                : token.Replace(",", "");
        }
        else if (commaCount > 0)
        {
            var lastComma = token.LastIndexOf(',');
            var fractionDigits = token.Length - lastComma - 1;

            // 2 casas: decimal; 3 casas: geralmente milhar em OCR fiscal
            normalized = fractionDigits == 2
                ? token.Replace(',', '.')
                : token.Replace(",", "");
        }
        else if (dotCount > 0)
        {
            var lastDot = token.LastIndexOf('.');
            var fractionDigits = token.Length - lastDot - 1;

            normalized = fractionDigits == 2
                ? token
                : token.Replace(".", "");
        }
        else
        {
            // Inteiros muito curtos geralmente não são valores finais de documento
            if (token.Length <= 2)
                return false;

            normalized = token;
        }

        return decimal.TryParse(normalized, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out value) && value > 0;
    }

    public async Task<ImportResultDto> ConfirmImportAsync(Guid userId, ConfirmImportDto dto)
    {
        var details = new List<string>();
        int debtsCreated = 0, cardPurchasesCreated = 0;

        foreach (var item in dto.Items)
        {
            try
            {
                if (item.Type == "card_purchase" && !string.IsNullOrEmpty(dto.CreditCardId))
                {
                    var purchaseDto = new CreateCardPurchaseDto(
                        Guid.Parse(dto.CreditCardId),
                        item.Description,
                        item.Amount,
                        DateOnly.TryParse(item.Date, out var purchaseDateOnly)
                            ? purchaseDateOnly
                            : DateOnly.FromDateTime(DateTime.UtcNow),
                        item.Installments,
                        null,
                        item.Category);
                    await _cardPurchaseService.CreateAsync(purchaseDto);
                    cardPurchasesCreated++;
                    details.Add($"✅ Compra '{item.Description}' R${item.Amount:F2} criada");
                }
                else
                {
                    var debtDto = new CreateDebtDto(
                        item.Description,
                        item.Amount,
                        DateOnly.TryParse(item.Date, out var dueDate) ? dueDate : DateOnly.FromDateTime(DateTime.UtcNow),
                        null,
                        item.Category);
                    await _debtService.CreateAsync(userId, debtDto);
                    debtsCreated++;
                    details.Add($"✅ Débito '{item.Description}' R${item.Amount:F2} criado");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao importar item: {Description}", item.Description);
                details.Add($"❌ Erro em '{item.Description}': {ex.Message}");
            }
        }

        return new ImportResultDto(dto.Items.Count, debtsCreated, cardPurchasesCreated, details);
    }

    private static string GetMimeType(string contentType, string fileName)
    {
        if (!string.IsNullOrEmpty(contentType) && contentType != "application/octet-stream")
            return contentType;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }

    // Gemini response models
    private record GeminiVisionResponse(List<GeminiVisionCandidate>? Candidates);
    private record GeminiVisionCandidate(GeminiVisionContent? Content);
    private record GeminiVisionContent(List<GeminiVisionPart>? Parts);
    private record GeminiVisionPart(string? Text);

    // OpenAI-compatible chat response model
    private record GenericChatResponse(List<GenericChoice>? Choices);
    private record GenericChoice(GenericMessage? Message);
    private record GenericMessage(string? Content);

    // OCR.Space response models
    private record OcrSpaceResponse(
        [property: JsonPropertyName("ParsedResults")] List<OcrSpaceParsedResult>? ParsedResults,
        [property: JsonPropertyName("IsErroredOnProcessing")] bool IsErroredOnProcessing,
        [property: JsonPropertyName("ErrorMessage")] List<string>? ErrorMessage);
    private record OcrSpaceParsedResult(
        [property: JsonPropertyName("ParsedText")] string? ParsedText);

    // Extraction models
    private record ExtractionResult(
        [property: JsonPropertyName("document_type")] string? DocumentType,
        string? Summary,
        List<ExtractionItem>? Items);
    private record ExtractionItem(
        string? Description,
        decimal Amount,
        string? Date,
        string? Category,
        string? Type,
        int Installments);
}
