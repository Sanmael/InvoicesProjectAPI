using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

public interface IDebtService
{
    Task<DebtDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<DebtDto>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<DebtDto>> GetPendingByUserIdAsync(Guid userId);
    Task<DebtDto> CreateAsync(Guid userId, CreateDebtDto dto);
    Task<IEnumerable<DebtDto>> CreateInstallmentAsync(Guid userId, CreateInstallmentDebtDto dto);
    Task<IEnumerable<DebtDto>> CreateRecurringAsync(Guid userId, CreateRecurringDebtDto dto);
    Task<DebtDto> UpdateAsync(Guid id, UpdateDebtDto dto);
    Task DeleteAsync(Guid id);
    Task DeleteGroupAsync(Guid groupId);
    Task<DebtDto> MarkAsPaidAsync(Guid id);
}
