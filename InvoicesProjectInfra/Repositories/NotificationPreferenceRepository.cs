using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;
using InvoicesProjectInfra.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoicesProjectInfra.Repositories;

public class NotificationPreferenceRepository : Repository<NotificationPreference>, INotificationPreferenceRepository
{
    public NotificationPreferenceRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<NotificationPreference?> GetByUserIdAsync(Guid userId)
    {
        return await _dbSet
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId);
    }

    public async Task<IEnumerable<NotificationPreference>> GetAllEnabledAsync()
    {
        return await _dbSet
            .Include(p => p.User)
            .Where(p => p.EmailNotificationsEnabled && p.User.IsActive)
            .ToListAsync();
    }
}
