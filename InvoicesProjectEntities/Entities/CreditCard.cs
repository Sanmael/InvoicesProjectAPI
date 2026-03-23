namespace InvoicesProjectEntities.Entities;

/// <summary>
/// Representa um cartão de crédito
/// </summary>
public class CreditCard : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string LastFourDigits { get; set; } = string.Empty;
    public decimal? CreditLimit { get; set; }
    public int ClosingDay { get; set; } // Dia do fechamento da fatura
    public int DueDay { get; set; } // Dia do vencimento da fatura
    public bool IsActive { get; set; } = true;

    // Foreign keys
    public Guid UserId { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual ICollection<CardPurchase> Purchases { get; set; } = new List<CardPurchase>();
}
