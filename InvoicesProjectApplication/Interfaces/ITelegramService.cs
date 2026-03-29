using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

public interface ITelegramService
{
    Task<TelegramLinkDto> GenerateLinkAsync(Guid userId);
    Task<TelegramStatusDto> GetStatusAsync(Guid userId);
    Task ToggleNotificationsAsync(Guid userId, bool enabled);
    Task UnlinkAsync(Guid userId);
    Task HandleWebhookAsync(TelegramWebhookUpdateDto update);
    Task SendMessageAsync(long chatId, string message);
}
