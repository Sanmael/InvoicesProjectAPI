using InvoicesProjectEntities.Entities;

namespace InvoicesProjectEntities.Interfaces;

public interface ICreditCardRepository : IRepository<CreditCard>
{
    Task<IEnumerable<CreditCard>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<CreditCard>> GetActiveByUserIdAsync(Guid userId);
    Task<CreditCard?> GetWithPurchasesAsync(Guid id);
}
