namespace InvoicesProjectEntities.Entities;

public class SavingsGoal : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ProductImageDataUrl { get; set; }
    public string? ProductUrl { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; } = 0;
    public DateOnly? Deadline { get; set; }
    public string Category { get; set; } = "Outros";
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }

    // FK
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;
}
