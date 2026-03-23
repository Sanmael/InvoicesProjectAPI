namespace InvoicesProjectEntities.Entities;

/// <summary>
/// Log de notificações por email enviadas.
/// Usado para evitar envio duplicado e para histórico.
/// </summary>
public class EmailNotification : BaseEntity
{
    public Guid UserId { get; set; }
    
    /// <summary>
    /// Tipo de notificação enviada
    /// </summary>
    public NotificationType Type { get; set; }
    
    /// <summary>
    /// Assunto do email
    /// </summary>
    public string Subject { get; set; } = string.Empty;
    
    /// <summary>
    /// Corpo do email (HTML)
    /// </summary>
    public string Body { get; set; } = string.Empty;
    
    /// <summary>
    /// Email do destinatário
    /// </summary>
    public string RecipientEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// Data/hora do envio
    /// </summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Se o email foi enviado com sucesso
    /// </summary>
    public bool WasSent { get; set; } = false;
    
    /// <summary>
    /// Mensagem de erro caso falhe o envio
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Referência para evitar duplicidade (ex: "LowBalance_2026-03" para saldo baixo de março/2026)
    /// </summary>
    public string? ReferenceKey { get; set; }
    
    // Navigation property
    public virtual User User { get; set; } = null!;
}

/// <summary>
/// Tipos de notificação
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Aviso de saldo baixo
    /// </summary>
    LowBalance = 1,
    
    /// <summary>
    /// Fatura do cartão fechando
    /// </summary>
    InvoiceClosing = 2,
    
    /// <summary>
    /// Limite do cartão próximo do máximo
    /// </summary>
    CardLimitNearMax = 3,
    
    /// <summary>
    /// Dívida próxima do vencimento
    /// </summary>
    UpcomingDebt = 4,
    
    /// <summary>
    /// Recebível próximo
    /// </summary>
    UpcomingReceivable = 5,
    
    /// <summary>
    /// Fatura do cartão fechou
    /// </summary>
    InvoiceClosed = 6
}
