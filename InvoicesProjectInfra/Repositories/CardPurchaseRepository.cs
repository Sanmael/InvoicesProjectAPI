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
        var purchases = await _dbSet
            .Include(p => p.CreditCard)
            .Where(p => p.CreditCard.UserId == userId)
            .OrderBy(p => p.PurchaseDate)
            .ToListAsync();

        return purchases.Where(p => IsBilledInMonth(p, p.CreditCard.ClosingDay, year, month));
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
        var purchases = await _dbSet
            .Include(p => p.CreditCard)
            .Where(p => p.CreditCardId == cardId)
            .OrderBy(p => p.PurchaseDate)
            .ToListAsync();

        return purchases.Where(p => IsBilledInMonth(p, p.CreditCard.ClosingDay, year, month));
    }

    private static bool IsBilledInMonth(CardPurchase purchase, int closingDay, int year, int month)
    {
        if (purchase.IsPaid)
            return false;

        // Determine first invoice month for this purchase based on closing day.
        var firstBilling = purchase.Installments <= 1 || purchase.PurchaseDate.Day <= closingDay
            ? new DateOnly(purchase.PurchaseDate.Year, purchase.PurchaseDate.Month, 1)
            : new DateOnly(purchase.PurchaseDate.Year, purchase.PurchaseDate.Month, 1).AddMonths(1);

        var target = new DateOnly(year, month, 1);
        for (int inst = purchase.CurrentInstallment; inst <= purchase.Installments; inst++)
        {
            var billedMonth = firstBilling.AddMonths(inst - 1);
            if (billedMonth.Year == target.Year && billedMonth.Month == target.Month)
                return true;
        }

        return false;
    }
}
