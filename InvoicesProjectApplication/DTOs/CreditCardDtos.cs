namespace InvoicesProjectApplication.DTOs;

public record CreditCardDto(
    Guid Id,
    string Name,
    string LastFourDigits,
    decimal? CreditLimit,
    int ClosingDay,
    int DueDay,
    bool IsActive,
    decimal TotalPending,
    decimal? AvailableLimit,
    DateTime CreatedAt
);

public record CreditCardWithPurchasesDto(
    Guid Id,
    string Name,
    string LastFourDigits,
    decimal? CreditLimit,
    int ClosingDay,
    int DueDay,
    bool IsActive,
    decimal TotalPending,
    IEnumerable<CardPurchaseDto> Purchases
);

public record CreateCreditCardDto(
    string Name,
    string LastFourDigits,
    decimal? CreditLimit,
    int ClosingDay,
    int DueDay
);

public record UpdateCreditCardDto(
    string? Name,
    decimal? CreditLimit,
    int? ClosingDay,
    int? DueDay,
    bool? IsActive
);
