using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Logging;

namespace InvoicesProjectInfra.Services;

/// <summary>
/// Serviço de envio de emails usando MailKit
/// </summary>
public class EmailSenderService : IEmailSenderService
{
    private readonly IEmailSettingsRepository _settingsRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<EmailSenderService> _logger;

    public EmailSenderService(
        IEmailSettingsRepository settingsRepository,
        IEncryptionService encryptionService,
        ILogger<EmailSenderService> logger)
    {
        _settingsRepository = settingsRepository;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<(bool Success, string? ErrorMessage)> SendEmailAsync(string to, string subject, string htmlBody)
    {
        try
        {
            var settings = await _settingsRepository.GetSettingsAsync();
            
            if (settings is null || !settings.IsConfigured)
            {
                return (false, "Configurações de email não encontradas ou não configuradas.");
            }

            var password = _encryptionService.Decrypt(settings.EncryptedPassword);
            
            if (string.IsNullOrEmpty(password))
            {
                return (false, "Senha de email não configurada.");
            }

            return await SendEmailAsync(
                settings.SmtpHost,
                settings.SmtpPort,
                settings.SenderEmail,
                settings.SenderName,
                password,
                settings.UseSsl,
                to,
                subject,
                htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar email para {To}", to);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> SendEmailAsync(
        string smtpHost,
        int smtpPort,
        string senderEmail,
        string senderName,
        string password,
        bool useSsl,
        string to,
        string subject,
        string htmlBody)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(new MailboxAddress("", to));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            
            var secureSocketOptions = useSsl 
                ? SecureSocketOptions.StartTls  // Para porta 587
                : SecureSocketOptions.Auto;

            await client.ConnectAsync(smtpHost, smtpPort, secureSocketOptions);
            await client.AuthenticateAsync(senderEmail, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email enviado com sucesso para {To}", to);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar email para {To}", to);
            return (false, ex.Message);
        }
    }
}
