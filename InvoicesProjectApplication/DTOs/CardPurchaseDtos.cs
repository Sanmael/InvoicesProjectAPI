namespace InvoicesProjectApplication.DTOs;

public record CardPurchaseDto(
    Guid Id,
    Guid CreditCardId,
    string Description,
    decimal Amount,
    DateTime PurchaseDate,
    int Installments,
    int CurrentInstallment,
    bool IsPaid,
    string? Notes,
    string Category,
    DateTime CreatedAt
);

public record CreateCardPurchaseDto(
    Guid CreditCardId,
    string Description,
    decimal Amount,
    DateTime PurchaseDate,
    int Installments,
    string? Notes,
    string? Category = null
);

public record UpdateCardPurchaseDto(
    string? Description,
    decimal? Amount,
    DateTime? PurchaseDate,
    int? Installments,
    bool? IsPaid,
    string? Notes,
    string? Category = null
);
