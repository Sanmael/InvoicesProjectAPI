using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;

namespace InvoicesProjectApplication.Services;

public class EmailSettingsService : IEmailSettingsService
{
    private readonly IEmailSettingsRepository _repository;
    private readonly IEncryptionService _encryptionService;
    private readonly IEmailSenderService _emailSenderService;

    public EmailSettingsService(
        IEmailSettingsRepository repository,
        IEncryptionService encryptionService,
        IEmailSenderService emailSenderService)
    {
        _repository = repository;
        _encryptionService = encryptionService;
        _emailSenderService = emailSenderService;
    }

    public async Task<EmailSettingsDto?> GetSettingsAsync()
    {
        var settings = await _repository.GetSettingsAsync();
        
        if (settings is null)
            return null;

        return new EmailSettingsDto(
            settings.Id,
            settings.SmtpHost,
            settings.SmtpPort,
            settings.SenderEmail,
            settings.SenderName,
            settings.UseSsl,
            settings.IsConfigured
        );
    }

    public async Task<EmailSettingsDto> SaveSettingsAsync(EmailSettingsCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SmtpHost))
            throw new InvalidOperationException("Servidor SMTP é obrigatório.");

        if (string.IsNullOrWhiteSpace(dto.SenderEmail))
            throw new InvalidOperationException("Email remetente é obrigatório.");

        if (dto.SmtpPort <= 0 || dto.SmtpPort > 65535)
            throw new InvalidOperationException("Porta SMTP inválida.");

        var existing = await _repository.GetSettingsAsync();

        var encryptedPassword = string.IsNullOrWhiteSpace(dto.Password)
            ? existing?.EncryptedPassword ?? throw new InvalidOperationException(
                "Informe a senha de app do email para a configuração inicial.")
            : _encryptionService.Encrypt(dto.Password.Trim());
        
        var settings = new EmailSettings
        {
            SmtpHost = dto.SmtpHost.Trim(),
            SmtpPort = dto.SmtpPort,
            SenderEmail = dto.SenderEmail.Trim().ToLowerInvariant(),
            SenderName = string.IsNullOrWhiteSpace(dto.SenderName)
                ? "Sistema de Faturas"
                : dto.SenderName.Trim(),
            EncryptedPassword = encryptedPassword,
            UseSsl = dto.UseSsl,
            IsConfigured = true
        };

        var saved = await _repository.SaveSettingsAsync(settings);

        return new EmailSettingsDto(
            saved.Id,
            saved.SmtpHost,
            saved.SmtpPort,
            saved.SenderEmail,
            saved.SenderName,
            saved.UseSsl,
            saved.IsConfigured
        );
    }

    public async Task UpdatePasswordAsync(string password)
    {
        var settings = await _repository.GetSettingsAsync();
        
        if (settings is null)
            throw new InvalidOperationException("Configurações de email não encontradas. Configure primeiro.");

        settings.EncryptedPassword = _encryptionService.Encrypt(password);
        settings.UpdatedAt = DateTime.UtcNow;
        
        await _repository.UpdateAsync(settings);
    }

    public async Task<EmailTestResultDto> TestConnectionAsync(EmailTestDto dto)
    {
        var settings = await _repository.GetSettingsAsync();
        
        if (settings is null)
            return new EmailTestResultDto(false, "Configurações de email não encontradas.");

        var password = _encryptionService.Decrypt(settings.EncryptedPassword);
        
        if (string.IsNullOrEmpty(password))
            return new EmailTestResultDto(false, "Senha não configurada.");

        var subject = dto.Subject ?? "Teste de Configuração de Email";
        var body = dto.Body ?? @"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <h2>Teste de Email</h2>
                <p>Este é um email de teste do Sistema de Faturas.</p>
                <p>Se você recebeu este email, suas configurações estão corretas!</p>
                <hr/>
                <small>Enviado em: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + @"</small>
            </body>
            </html>";

        var result = await _emailSenderService.SendEmailAsync(
            settings.SmtpHost,
            settings.SmtpPort,
            settings.SenderEmail,
            settings.SenderName,
            password,
            settings.UseSsl,
            dto.TestEmail,
            subject,
            body);

        return new EmailTestResultDto(
            result.Success,
            result.Success ? "Email de teste enviado com sucesso!" : result.ErrorMessage ?? "Erro desconhecido"
        );
    }
}
