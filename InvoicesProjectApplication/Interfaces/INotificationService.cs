using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

/// <summary>
/// Serviço principal de notificações - verifica condições e envia emails
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Processa notificações para todos os usuários com notificações habilitadas
    /// </summary>
    Task ProcessAllNotificationsAsync();
    
    /// <summary>
    /// Processa notificações para um usuário específico
    /// </summary>
    Task ProcessNotificationsForUserAsync(Guid userId);
    
    /// <summary>
    /// Obtém histórico de notificações do usuário
    /// </summary>
    Task<IEnumerable<EmailNotificationDto>> GetNotificationHistoryAsync(Guid userId, int limit = 50);
    
    /// <summary>
    /// Obtém resumo das notificações
    /// </summary>
    Task<NotificationSummaryDto> GetNotificationSummaryAsync(Guid userId);
}
