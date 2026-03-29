using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;
using InvoicesProjectInfra.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoicesProjectInfra.Repositories;

public class SavingsGoalRepository : Repository<SavingsGoal>, ISavingsGoalRepository
{
    public SavingsGoalRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<SavingsGoal>> GetByUserIdAsync(Guid userId)
    {
        return await _dbSet
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<SavingsGoal>> GetActiveByUserIdAsync(Guid userId)
    {
        return await _dbSet
            .Where(g => g.UserId == userId && !g.IsCompleted)
            .OrderBy(g => g.Deadline)
            .ToListAsync();
    }
}
