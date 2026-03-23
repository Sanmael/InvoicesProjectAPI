using InvoicesProjectEntities.Entities;

namespace InvoicesProjectEntities.Interfaces;

public interface IDebtRepository : IRepository<Debt>
{
    Task<IEnumerable<Debt>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<Debt>> GetPendingByUserIdAsync(Guid userId);
    Task<IEnumerable<Debt>> GetByUserIdAndMonthAsync(Guid userId, int year, int month);
    Task<decimal> GetTotalPendingByUserIdAsync(Guid userId);
    Task<IEnumerable<Debt>> GetByInstallmentGroupAsync(Guid groupId);
}
