using System.Text.Json.Serialization;

namespace InvoicesProjectApplication.DTOs;

/// <summary>
/// Mensagem recebida do WhatsApp, já normalizada (agnóstica de provider).
/// </summary>
public record WhatsAppIncomingMessage(
    string PhoneNumber,
    string Text,
    string? SenderName
);

/// <summary>
/// Resposta enviada de volta ao WhatsApp.
/// </summary>
public record WhatsAppOutgoingMessage(
    string PhoneNumber,
    string Text
);

/// <summary>
/// Status da vinculação do WhatsApp do usuário.
/// </summary>
public record WhatsAppStatusDto(
    bool IsLinked,
    string? PhoneNumber
);

/// <summary>
/// DTO para vincular número de WhatsApp à conta via API autenticada.
/// </summary>
public record WhatsAppLinkDto(
    string PhoneNumber
);
