namespace InvoicesProjectEntities.Entities;

/// <summary>
/// Representa um saldo a receber
/// </summary>
public class Receivable : BaseEntity
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly ExpectedDate { get; set; }
    public bool IsReceived { get; set; } = false;
    public DateTime? ReceivedAt { get; set; }
    public string? Notes { get; set; }

    // Recorrência (ex: salário mensal)
    public bool IsRecurring { get; set; } = false;
    public int? RecurringDay { get; set; }  // Dia do mês para receber (1-28)
    public Guid? RecurrenceGroupId { get; set; }  // Agrupa entradas da mesma recorrência

    // Foreign keys
    public Guid UserId { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
}
