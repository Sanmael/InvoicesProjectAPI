using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

public interface IReceivableService
{
    Task<ReceivableDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<ReceivableDto>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<ReceivableDto>> GetPendingByUserIdAsync(Guid userId);
    Task<ReceivableDto> CreateAsync(Guid userId, CreateReceivableDto dto);
    Task<IEnumerable<ReceivableDto>> CreateRecurringAsync(Guid userId, CreateRecurringReceivableDto dto);
    Task<ReceivableDto> UpdateAsync(Guid id, UpdateReceivableDto dto);
    Task DeleteAsync(Guid id);
    Task DeleteGroupAsync(Guid groupId);
    Task<ReceivableDto> MarkAsReceivedAsync(Guid id);
}
