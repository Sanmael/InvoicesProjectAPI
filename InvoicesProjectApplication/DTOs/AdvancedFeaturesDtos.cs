namespace InvoicesProjectApplication.DTOs;

// ===== SavingsGoal DTOs =====

public record SavingsGoalDto(
    Guid Id,
    string Title,
    string? Description,
    decimal TargetAmount,
    decimal CurrentAmount,
    DateOnly? Deadline,
    string Category,
    bool IsCompleted,
    DateTime? CompletedAt,
    decimal ProgressPercent,
    DateTime CreatedAt);

public record CreateSavingsGoalDto(
    string Title,
    string? Description,
    decimal TargetAmount,
    decimal CurrentAmount,
    DateOnly? Deadline,
    string? Category);

public record UpdateSavingsGoalDto(
    string? Title,
    string? Description,
    decimal? TargetAmount,
    decimal? CurrentAmount,
    DateOnly? Deadline,
    string? Category);

public record AddSavingsAmountDto(decimal Amount);

// ===== Best Card Recommendation =====

public record BestCardRecommendationDto(
    Guid CardId,
    string CardName,
    string LastFourDigits,
    int ClosingDay,
    int DueDay,
    int DaysUntilPayment,
    DateOnly NextClosingDate,
    DateOnly InvoiceDueDate,
    string Explanation);

// ===== Anticipation Simulation =====

public record AnticipationSimulationRequestDto(
    decimal MonthlyDiscountRate);

public record AnticipationInstallmentDto(
    int InstallmentNumber,
    decimal OriginalValue,
    decimal DiscountedValue,
    decimal Savings);

public record AnticipationSimulationDto(
    Guid PurchaseId,
    string Description,
    int TotalInstallments,
    int RemainingInstallments,
    decimal InstallmentValue,
    decimal TotalRemaining,
    decimal TotalDiscounted,
    decimal TotalSavings,
    decimal DiscountRate,
    IEnumerable<AnticipationInstallmentDto> Installments);

// ===== Financial Health Score =====

public record FinancialScoreDto(
    int TotalScore,
    string Classification,
    FinancialScoreBreakdownDto Breakdown,
    IEnumerable<string> Tips);

public record FinancialScoreBreakdownDto(
    int PaymentDiscipline,
    int PaymentDisciplineMax,
    int CreditUtilization,
    int CreditUtilizationMax,
    int SavingsRate,
    int SavingsRateMax,
    int GoalProgress,
    int GoalProgressMax,
    int FinancialOrganization,
    int FinancialOrganizationMax);
