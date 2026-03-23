using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;
using InvoicesProjectInfra.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoicesProjectInfra.Repositories;

public class EmailSettingsRepository : Repository<EmailSettings>, IEmailSettingsRepository
{
    public EmailSettingsRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<EmailSettings?> GetSettingsAsync()
    {
        // Retorna o primeiro registro (singleton)
        return await _dbSet.FirstOrDefaultAsync();
    }

    public async Task<EmailSettings> SaveSettingsAsync(EmailSettings settings)
    {
        var existing = await GetSettingsAsync();
        
        if (existing is null)
        {
            return await AddAsync(settings);
        }
        
        existing.SmtpHost = settings.SmtpHost;
        existing.SmtpPort = settings.SmtpPort;
        existing.SenderEmail = settings.SenderEmail;
        existing.SenderName = settings.SenderName;
        existing.EncryptedPassword = settings.EncryptedPassword;
        existing.UseSsl = settings.UseSsl;
        existing.IsConfigured = settings.IsConfigured;
        existing.UpdatedAt = DateTime.UtcNow;
        
        return await UpdateAsync(existing);
    }
}
