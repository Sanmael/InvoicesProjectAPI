using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;
using InvoicesProjectInfra.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoicesProjectInfra.Repositories;

public class EmailNotificationRepository : Repository<EmailNotification>, IEmailNotificationRepository
{
    public EmailNotificationRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<EmailNotification>> GetByUserIdAsync(Guid userId)
    {
        return await _dbSet
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.SentAt)
            .ToListAsync();
    }

    public async Task<bool> ExistsByReferenceKeyAsync(Guid userId, string referenceKey)
    {
        return await _dbSet.AnyAsync(n => n.UserId == userId && n.ReferenceKey == referenceKey && n.WasSent);
    }

    public async Task<IEnumerable<EmailNotification>> GetPendingAsync()
    {
        return await _dbSet
            .Where(n => !n.WasSent && n.ErrorMessage == null)
            .Include(n => n.User)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmailNotification>> GetByUserIdAndPeriodAsync(Guid userId, DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(n => n.UserId == userId && n.SentAt >= startDate && n.SentAt <= endDate)
            .OrderByDescending(n => n.SentAt)
            .ToListAsync();
    }
}
