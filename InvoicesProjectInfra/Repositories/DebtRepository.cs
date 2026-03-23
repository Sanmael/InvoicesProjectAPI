using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;
using InvoicesProjectInfra.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoicesProjectInfra.Repositories;

public class DebtRepository : Repository<Debt>, IDebtRepository
{
    public DebtRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Debt>> GetByUserIdAsync(Guid userId)
    {
        return await _dbSet
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Debt>> GetPendingByUserIdAsync(Guid userId)
    {
        return await _dbSet
            .Where(d => d.UserId == userId && !d.IsPaid)
            .OrderBy(d => d.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Debt>> GetByUserIdAndMonthAsync(Guid userId, int year, int month)
    {
        return await _dbSet
            .Where(d => d.UserId == userId &&
                        d.DueDate.Year == year &&
                        d.DueDate.Month == month)
            .OrderBy(d => d.DueDate)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalPendingByUserIdAsync(Guid userId)
    {
        return await _dbSet
            .Where(d => d.UserId == userId && !d.IsPaid)
            .SumAsync(d => d.Amount);
    }

    public async Task<IEnumerable<Debt>> GetByInstallmentGroupAsync(Guid groupId)
    {
        return await _dbSet
            .Where(d => d.InstallmentGroupId == groupId)
            .OrderBy(d => d.InstallmentNumber)
            .ToListAsync();
    }
}
