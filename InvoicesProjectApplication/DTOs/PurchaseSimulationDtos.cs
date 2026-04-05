namespace InvoicesProjectApplication.DTOs;

// ===== Request =====

public record SimulationItemDto(
    string Description,
    int Quantity,
    decimal UnitPrice
);

public record SimulationAllocationDto(
    Guid CreditCardId,
    decimal Amount,
    int Installments
);

public record SimulationPlanRequestDto(
    string Label,
    IEnumerable<SimulationAllocationDto> Allocations
);

public record PurchaseSimulationRequestDto(
    IEnumerable<SimulationItemDto> Items,
    IEnumerable<SimulationPlanRequestDto> Plans,
    int ProjectionMonths = 12,
    int? StartYear = null,
    int? StartMonth = null
);

// ===== Response =====

public record SimulationItemResultDto(
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal
);

public record SimulationCardInfoDto(
    Guid Id,
    string Name,
    string LastFourDigits,
    decimal? CreditLimit,
    decimal TotalPending,
    decimal? AvailableLimit
);

public record SimulationAllocationResultDto(
    Guid CreditCardId,
    string CardName,
    string LastFourDigits,
    decimal Amount,
    int Installments,
    decimal InstallmentValue,
    decimal? AvailableLimit,
    bool ExceedsLimit
);

public record SimulationMonthProjectionDto(
    int Year,
    int Month,
    string Label,
    decimal TotalReceivables,
    decimal ExistingDebts,
    decimal ExistingCardPurchases,
    decimal SimulatedInstallment,
    decimal TotalExpenses,
    decimal Balance
);

public record SimulationPlanResultDto(
    string Label,
    IEnumerable<SimulationAllocationResultDto> Allocations,
    decimal TotalAllocated,
    decimal Unallocated,
    bool HasLimitIssues,
    IEnumerable<SimulationMonthProjectionDto> MonthlyProjections
);

public record PurchaseSimulationResultDto(
    IEnumerable<SimulationItemResultDto> Items,
    decimal TotalAmount,
    IEnumerable<SimulationCardInfoDto> AvailableCards,
    IEnumerable<SimulationPlanResultDto> Plans
);
