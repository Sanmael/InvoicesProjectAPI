using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;
using InvoicesProjectInfra.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoicesProjectInfra.Repositories;

public class CreditCardRepository : Repository<CreditCard>, ICreditCardRepository
{
    public CreditCardRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<CreditCard>> GetByUserIdAsync(Guid userId)
    {
        return await _dbSet
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<CreditCard>> GetActiveByUserIdAsync(Guid userId)
    {
        return await _dbSet
            .Where(c => c.UserId == userId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<CreditCard?> GetWithPurchasesAsync(Guid id)
    {
        return await _dbSet
            .Include(c => c.Purchases)
            .FirstOrDefaultAsync(c => c.Id == id);
    }
}
