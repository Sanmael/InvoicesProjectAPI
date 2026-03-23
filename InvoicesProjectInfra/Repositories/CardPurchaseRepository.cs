using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;
using InvoicesProjectInfra.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoicesProjectInfra.Repositories;

public class CardPurchaseRepository : Repository<CardPurchase>, ICardPurchaseRepository
{
    public CardPurchaseRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<CardPurchase>> GetByCreditCardIdAsync(Guid creditCardId)
    {
        return await _dbSet
            .Where(p => p.CreditCardId == creditCardId)
            .OrderByDescending(p => p.PurchaseDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CardPurchase>> GetPendingByCreditCardIdAsync(Guid creditCardId)
    {
        return await _dbSet
            .Where(p => p.CreditCardId == creditCardId && !p.IsPaid)
            .OrderBy(p => p.PurchaseDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CardPurchase>> GetByUserIdAndMonthAsync(Guid userId, int year, int month)
    {
        return await _dbSet
            .Include(p => p.CreditCard)
            .Where(p => p.CreditCard.UserId == userId &&
                        p.PurchaseDate.Year == year &&
                        p.PurchaseDate.Month == month)
            .OrderBy(p => p.PurchaseDate)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalPendingByCardIdAsync(Guid creditCardId)
    {
        return await _dbSet
            .Where(p => p.CreditCardId == creditCardId && !p.IsPaid)
            .SumAsync(p => p.Amount);
    }

    public async Task<IEnumerable<CardPurchase>> GetByCardIdAsync(Guid cardId)
    {
        return await _dbSet
            .Where(p => p.CreditCardId == cardId)
            .OrderByDescending(p => p.PurchaseDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CardPurchase>> GetByCardIdAndMonthAsync(Guid cardId, int year, int month)
    {
        return await _dbSet
            .Where(p => p.CreditCardId == cardId &&
                        p.PurchaseDate.Year == year &&
                        p.PurchaseDate.Month == month)
            .OrderBy(p => p.PurchaseDate)
            .ToListAsync();
    }
}
