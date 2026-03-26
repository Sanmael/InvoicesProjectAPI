namespace InvoicesProjectApplication.DTOs;

public record DebtDto(
    Guid Id,
    string Description,
    decimal Amount,
    DateOnly DueDate,
    bool IsPaid,
    DateTime? PaidAt,
    string? Notes,
    string Category,
    bool IsInstallment,
    int? TotalInstallments,
    int? InstallmentNumber,
    Guid? InstallmentGroupId,
    DateTime CreatedAt
);

public record CreateDebtDto(
    string Description,
    decimal Amount,
    DateOnly DueDate,
    string? Notes,
    string? Category = null
);

/// <summary>
/// Cria uma dívida parcelada (ex: dívida informal paga em parcelas mensais).
/// O valor informado é o total; cada parcela = TotalAmount / Installments.
/// </summary>
public record CreateInstallmentDebtDto(
    string Description,
    decimal TotalAmount,    // Valor total da dívida
    DateOnly FirstDueDate,  // Vencimento da 1ª parcela
    int Installments,       // Número de parcelas (máx 60)
    string? Notes,
    string? Category = null
);

/// <summary>
/// Cria um débito recorrente mensal (ex: ajuda mensal) para os próximos N meses.
/// </summary>
public record CreateRecurringDebtDto(
    string Description,
    decimal Amount,
    int RecurringDay,
    int Months,
    DateOnly? StartDate,
    string? Notes,
    string? Category = null
);

public record UpdateDebtDto(
    string? Description,
    decimal? Amount,
    DateOnly? DueDate,
    bool? IsPaid,
    string? Notes,
    string? Category = null
);
