using InvoicesProjectEntities.Entities;

namespace InvoicesProjectEntities.Interfaces;

public interface IEmailSettingsRepository : IRepository<EmailSettings>
{
    /// <summary>
    /// Obtém as configurações de email do sistema (singleton)
    /// </summary>
    Task<EmailSettings?> GetSettingsAsync();
    
    /// <summary>
    /// Salva ou atualiza as configurações de email
    /// </summary>
    Task<EmailSettings> SaveSettingsAsync(EmailSettings settings);
}
