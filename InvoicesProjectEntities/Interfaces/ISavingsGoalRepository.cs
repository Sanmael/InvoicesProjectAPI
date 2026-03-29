using InvoicesProjectEntities.Entities;

namespace InvoicesProjectEntities.Interfaces;

public interface ISavingsGoalRepository : IRepository<SavingsGoal>
{
    Task<IEnumerable<SavingsGoal>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<SavingsGoal>> GetActiveByUserIdAsync(Guid userId);
}
