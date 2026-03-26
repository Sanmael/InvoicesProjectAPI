namespace InvoicesProjectEntities.Entities;

/// <summary>
/// Representa um saldo devedor (conta a pagar)
/// </summary>
public class Debt : BaseEntity
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public bool IsPaid { get; set; } = false;
    public DateTime? PaidAt { get; set; }
    public string? Notes { get; set; }
    public string Category { get; set; } = "Outros";

    // Parcelamento (ex: dívida informal parcelada)
    public bool IsInstallment { get; set; } = false;
    public int? TotalInstallments { get; set; }   // Total de parcelas
    public int? InstallmentNumber { get; set; }   // Número desta parcela (1, 2, 3...)
    public Guid? InstallmentGroupId { get; set; } // Agrupa todas as parcelas da mesma dívida

    // Foreign keys
    public Guid UserId { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
}
