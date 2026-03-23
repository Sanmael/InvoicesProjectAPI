using InvoicesProjectEntities.Entities;

namespace InvoicesProjectEntities.Interfaces;

public interface IEmailNotificationRepository : IRepository<EmailNotification>
{
    /// <summary>
    /// Obtém notificações de um usuário
    /// </summary>
    Task<IEnumerable<EmailNotification>> GetByUserIdAsync(Guid userId);
    
    /// <summary>
    /// Verifica se já existe uma notificação com a chave de referência
    /// </summary>
    Task<bool> ExistsByReferenceKeyAsync(Guid userId, string referenceKey);
    
    /// <summary>
    /// Obtém notificações pendentes de envio
    /// </summary>
    Task<IEnumerable<EmailNotification>> GetPendingAsync();
    
    /// <summary>
    /// Obtém notificações de um usuário em um período
    /// </summary>
    Task<IEnumerable<EmailNotification>> GetByUserIdAndPeriodAsync(Guid userId, DateTime startDate, DateTime endDate);
}
