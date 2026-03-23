namespace InvoicesProjectApplication.Interfaces;

/// <summary>
/// Serviço de baixo nível para envio de emails via SMTP
/// </summary>
public interface IEmailSenderService
{
    /// <summary>
    /// Envia um email
    /// </summary>
    /// <param name="to">Email do destinatário</param>
    /// <param name="subject">Assunto</param>
    /// <param name="htmlBody">Corpo em HTML</param>
    /// <returns>True se enviou com sucesso</returns>
    Task<(bool Success, string? ErrorMessage)> SendEmailAsync(string to, string subject, string htmlBody);
    
    /// <summary>
    /// Envia email com configurações específicas (para testes)
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> SendEmailAsync(
        string smtpHost,
        int smtpPort,
        string senderEmail,
        string senderName,
        string password,
        bool useSsl,
        string to,
        string subject,
        string htmlBody);
}
