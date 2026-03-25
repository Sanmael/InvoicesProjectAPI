namespace InvoicesProjectApplication.DTOs;

public record ReceivableDto(
    Guid Id,
    string Description,
    decimal Amount,
    DateOnly ExpectedDate,
    bool IsReceived,
    DateTime? ReceivedAt,
    string? Notes,
    bool IsRecurring,
    int? RecurringDay,
    Guid? RecurrenceGroupId,
    DateTime CreatedAt
);

public record CreateReceivableDto(
    string Description,
    decimal Amount,
    DateOnly ExpectedDate,
    string? Notes
);

/// <summary>
/// Cria um recebível recorrente (ex: salário) para os próximos N meses.
/// </summary>
public record CreateRecurringReceivableDto(
    string Description,
    decimal Amount,
    int RecurringDay,   // Dia do mês (1-28)
    string? Notes,
    int Months = 12     // Quantos meses gerar, padrão 12
);

public record UpdateReceivableDto(
    string? Description,
    decimal? Amount,
    DateOnly? ExpectedDate,
    bool? IsReceived,
    string? Notes
);
