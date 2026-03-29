namespace InvoicesProjectApplication.DTOs;

public record ExtractedItemDto(
    string Description,
    decimal Amount,
    string Date,
    string? Category,
    string Type, // "debt" or "card_purchase"
    int Installments
);

public record DocumentExtractionResultDto(
    string FileName,
    string DocumentType,
    List<ExtractedItemDto> Items,
    string? Summary
);

public record ConfirmImportDto(
    List<ExtractedItemDto> Items,
    string? CreditCardId
);

public record ImportResultDto(
    int TotalItems,
    int DebtsCreated,
    int CardPurchasesCreated,
    List<string> Details
);
