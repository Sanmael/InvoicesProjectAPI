using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

public interface IChatService
{
    Task<ChatResponseDto> ProcessMessageAsync(Guid userId, ChatRequestDto request);
}
