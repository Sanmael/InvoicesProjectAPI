using System.Net.Http.Json;
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
    private readonly string _geminiApiKey;
    private readonly string _geminiBaseUrl;
    private readonly string _geminiModel;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

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

        // Gemini Vision é o provider preferido para OCR
        var geminiSection = configuration.GetSection("ChatProvider:Providers:Gemini");
        _geminiApiKey = geminiSection["ApiKey"]
            ?? throw new InvalidOperationException("Gemini ApiKey não configurada para importação de documentos.");
        _geminiBaseUrl = geminiSection["BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta";
        _geminiModel = geminiSection["Model"] ?? "gemini-2.0-flash";
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

        // Limpa possível markdown wrapping
        textContent = textContent.Trim();
        if (textContent.StartsWith("```json")) textContent = textContent[7..];
        if (textContent.StartsWith("```")) textContent = textContent[3..];
        if (textContent.EndsWith("```")) textContent = textContent[..^3];
        textContent = textContent.Trim();

        var extraction = JsonSerializer.Deserialize<ExtractionResult>(textContent, JsonOptions);

        if (extraction is null)
            throw new InvalidOperationException("Resposta da IA não pôde ser interpretada.");

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
