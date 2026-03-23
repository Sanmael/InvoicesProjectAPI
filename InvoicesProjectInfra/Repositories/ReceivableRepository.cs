using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;
using InvoicesProjectInfra.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoicesProjectInfra.Repositories;

public class ReceivableRepository : Repository<Receivable>, IReceivableRepository
{
    public ReceivableRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Receivable>> GetByUserIdAsync(Guid userId)
    {
        return await _dbSet
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ExpectedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Receivable>> GetPendingByUserIdAsync(Guid userId)
    {
        return await _dbSet
            .Where(r => r.UserId == userId && !r.IsReceived)
            .OrderBy(r => r.ExpectedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Receivable>> GetByUserIdAndMonthAsync(Guid userId, int year, int month)
    {
        return await _dbSet
            .Where(r => r.UserId == userId &&
                        r.ExpectedDate.Year == year &&
                        r.ExpectedDate.Month == month)
            .OrderBy(r => r.ExpectedDate)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalPendingByUserIdAsync(Guid userId)
    {
        return await _dbSet
            .Where(r => r.UserId == userId && !r.IsReceived)
            .SumAsync(r => r.Amount);
    }

    public async Task<IEnumerable<Receivable>> GetByRecurrenceGroupAsync(Guid groupId)
    {
        return await _dbSet
            .Where(r => r.RecurrenceGroupId == groupId)
            .OrderBy(r => r.ExpectedDate)
            .ToListAsync();
    }
}
