namespace InvoicesProjectEntities.Entities;

/// <summary>
/// Representa uma compra no cartão de crédito
/// </summary>
public class CardPurchase : BaseEntity
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly PurchaseDate { get; set; }
    public int Installments { get; set; } = 1; // Número de parcelas
    public int CurrentInstallment { get; set; } = 1; // Parcela atual
    public bool IsPaid { get; set; } = false;
    public string? Notes { get; set; }
    public string Category { get; set; } = "Outros";

    // Foreign keys
    public Guid CreditCardId { get; set; }
    public Guid? TagEventoId { get; set; } // Agrupamento temporal (opcional)

    // Navigation properties
    public virtual CreditCard CreditCard { get; set; } = null!;
    public virtual TagEvento? TagEvento { get; set; }
}
