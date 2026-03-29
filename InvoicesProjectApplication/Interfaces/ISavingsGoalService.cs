using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

public interface ISavingsGoalService
{
    Task<SavingsGoalDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<SavingsGoalDto>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<SavingsGoalDto>> GetActiveByUserIdAsync(Guid userId);
    Task<SavingsGoalDto> CreateAsync(Guid userId, CreateSavingsGoalDto dto);
    Task<SavingsGoalDto> UpdateAsync(Guid id, UpdateSavingsGoalDto dto);
    Task<SavingsGoalDto> AddAmountAsync(Guid id, decimal amount);
    Task DeleteAsync(Guid id);
}
