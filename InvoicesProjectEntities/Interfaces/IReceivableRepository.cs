using InvoicesProjectEntities.Entities;

namespace InvoicesProjectEntities.Interfaces;

public interface IReceivableRepository : IRepository<Receivable>
{
    Task<IEnumerable<Receivable>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<Receivable>> GetPendingByUserIdAsync(Guid userId);
    Task<IEnumerable<Receivable>> GetByUserIdAndMonthAsync(Guid userId, int year, int month);
    Task<decimal> GetTotalPendingByUserIdAsync(Guid userId);
    Task<IEnumerable<Receivable>> GetByRecurrenceGroupAsync(Guid groupId);
}
