using InvoicesProjectEntities.Entities;

namespace InvoicesProjectEntities.Interfaces;

public interface ICardPurchaseRepository : IRepository<CardPurchase>
{
    Task<IEnumerable<CardPurchase>> GetByCreditCardIdAsync(Guid creditCardId);
    Task<IEnumerable<CardPurchase>> GetPendingByCreditCardIdAsync(Guid creditCardId);
    Task<IEnumerable<CardPurchase>> GetByUserIdAndMonthAsync(Guid userId, int year, int month);
    Task<decimal> GetTotalPendingByCardIdAsync(Guid creditCardId);
    Task<IEnumerable<CardPurchase>> GetByCardIdAsync(Guid cardId);
    Task<IEnumerable<CardPurchase>> GetByCardIdAndMonthAsync(Guid cardId, int year, int month);
}
