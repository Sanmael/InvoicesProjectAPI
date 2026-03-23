namespace InvoicesProjectEntities.Entities;

/// <summary>
/// Preferências de notificação do usuário.
/// Cada usuário terá um registro com suas preferências.
/// </summary>
public class NotificationPreference : BaseEntity
{
    public Guid UserId { get; set; }
    
    /// <summary>
    /// Notificar quando o saldo estiver baixo
    /// </summary>
    public bool NotifyLowBalance { get; set; } = true;
    
    /// <summary>
    /// Valor mínimo de saldo para disparar notificação de saldo baixo.
    /// Se o saldo ficar abaixo desse valor, envia notificação.
    /// </summary>
    public decimal LowBalanceThreshold { get; set; } = 500;
    
    /// <summary>
    /// Notificar quando a fatura do cartão fechar
    /// </summary>
    public bool NotifyInvoiceClosed { get; set; } = true;
    
    /// <summary>
    /// Quantos dias antes do fechamento da fatura enviar aviso
    /// </summary>
    public int DaysBeforeInvoiceCloseNotification { get; set; } = 3;
    
    /// <summary>
    /// Notificar quando o limite do cartão estiver próximo de estourar
    /// </summary>
    public bool NotifyCardLimitNearMax { get; set; } = true;
    
    /// <summary>
    /// Percentual de uso do limite que dispara a notificação (0-100)
    /// Ex: 80 = notifica quando usar 80% do limite
    /// </summary>
    public int CardLimitWarningPercentage { get; set; } = 80;
    
    /// <summary>
    /// Notificar sobre dívidas próximas do vencimento
    /// </summary>
    public bool NotifyUpcomingDebts { get; set; } = true;
    
    /// <summary>
    /// Quantos dias antes do vencimento da dívida enviar aviso
    /// </summary>
    public int DaysBeforeDebtDueNotification { get; set; } = 5;
    
    /// <summary>
    /// Notificar sobre recebíveis próximos
    /// </summary>
    public bool NotifyUpcomingReceivables { get; set; } = true;
    
    /// <summary>
    /// Quantos dias antes do recebível enviar lembrete
    /// </summary>
    public int DaysBeforeReceivableNotification { get; set; } = 3;
    
    /// <summary>
    /// Se as notificações por email estão habilitadas para este usuário
    /// </summary>
    public bool EmailNotificationsEnabled { get; set; } = true;
    
    // Navigation property
    public virtual User User { get; set; } = null!;
}
