namespace InvoicesProjectApplication.DTOs;

/// <summary>
/// DTO para exibição das configurações de email (sem senha)
/// </summary>
public record EmailSettingsDto(
    Guid Id,
    string SmtpHost,
    int SmtpPort,
    string SenderEmail,
    string SenderName,
    bool UseSsl,
    bool IsConfigured
);

/// <summary>
/// DTO para criar/atualizar configurações de email
/// </summary>
public record EmailSettingsCreateDto(
    string SmtpHost,
    int SmtpPort,
    string SenderEmail,
    string SenderName,
    string Password,
    bool UseSsl
);

/// <summary>
/// DTO para atualizar apenas a senha
/// </summary>
public record EmailSettingsPasswordUpdateDto(
    string Password
);

/// <summary>
/// DTO para testar conexão SMTP
/// </summary>
public record EmailTestDto(
    string TestEmail,
    string? Subject,
    string? Body
);

/// <summary>
/// Resultado do teste de email
/// </summary>
public record EmailTestResultDto(
    bool Success,
    string Message
);
