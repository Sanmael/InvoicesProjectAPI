namespace InvoicesProjectEntities.Entities;

public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Se o usuário é admin do sistema
    /// </summary>
    public bool IsAdmin { get; set; } = false;

    // Navigation properties
    public virtual ICollection<Debt> Debts { get; set; } = new List<Debt>();
    public virtual ICollection<Receivable> Receivables { get; set; } = new List<Receivable>();
    public virtual ICollection<CreditCard> CreditCards { get; set; } = new List<CreditCard>();
    public virtual NotificationPreference? NotificationPreference { get; set; }
    public virtual ICollection<EmailNotification> EmailNotifications { get; set; } = new List<EmailNotification>();
    public virtual ICollection<SavingsGoal> SavingsGoals { get; set; } = new List<SavingsGoal>();
}
