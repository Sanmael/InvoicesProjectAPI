using InvoicesProjectEntities.Entities;

namespace InvoicesProjectEntities.Interfaces;

public interface INotificationPreferenceRepository : IRepository<NotificationPreference>
{
    /// <summary>
    /// Obtém as preferências de notificação de um usuário
    /// </summary>
    Task<NotificationPreference?> GetByUserIdAsync(Guid userId);
    
    /// <summary>
    /// Obtém todos os usuários com notificações habilitadas
    /// </summary>
    Task<IEnumerable<NotificationPreference>> GetAllEnabledAsync();

    Task<NotificationPreference?> GetByLinkTokenAsync(string token);
    Task<NotificationPreference?> GetByChatIdAsync(long chatId);
    Task<IEnumerable<NotificationPreference>> GetAllTelegramEnabledAsync();
}
