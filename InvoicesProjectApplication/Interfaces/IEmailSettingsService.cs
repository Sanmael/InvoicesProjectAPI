using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

/// <summary>
/// Serviço para gerenciamento das configurações de email do sistema
/// </summary>
public interface IEmailSettingsService
{
    /// <summary>
    /// Obtém as configurações atuais de email
    /// </summary>
    Task<EmailSettingsDto?> GetSettingsAsync();
    
    /// <summary>
    /// Cria ou atualiza as configurações de email
    /// </summary>
    Task<EmailSettingsDto> SaveSettingsAsync(EmailSettingsCreateDto dto);
    
    /// <summary>
    /// Atualiza apenas a senha
    /// </summary>
    Task UpdatePasswordAsync(string password);
    
    /// <summary>
    /// Testa a configuração de email enviando um email de teste
    /// </summary>
    Task<EmailTestResultDto> TestConnectionAsync(EmailTestDto dto);
}
