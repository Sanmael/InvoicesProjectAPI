using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

/// <summary>
/// Serviço para gerenciamento de preferências de notificação
/// </summary>
public interface INotificationPreferenceService
{
    /// <summary>
    /// Obtém as preferências de notificação do usuário
    /// </summary>
    Task<NotificationPreferenceDto?> GetByUserIdAsync(Guid userId);
    
    /// <summary>
    /// Cria ou atualiza as preferências de notificação do usuário
    /// </summary>
    Task<NotificationPreferenceDto> SaveAsync(Guid userId, NotificationPreferenceCreateDto dto);
    
    /// <summary>
    /// Obtém as preferências padrão (para novos usuários)
    /// </summary>
    NotificationPreferenceDto GetDefaults();
}
