namespace InvoicesProjectEntities.Entities;

/// <summary>
/// Representa uma compra no cartão de crédito
/// </summary>
public class CardPurchase : BaseEntity
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime PurchaseDate { get; set; }
    public int Installments { get; set; } = 1; // Número de parcelas
    public int CurrentInstallment { get; set; } = 1; // Parcela atual
    public bool IsPaid { get; set; } = false;
    public string? Notes { get; set; }

    // Foreign keys
    public Guid CreditCardId { get; set; }

    // Navigation properties
    public virtual CreditCard CreditCard { get; set; } = null!;
}
