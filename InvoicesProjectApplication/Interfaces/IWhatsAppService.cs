using System.Text.Json;
using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

/// <summary>
/// Lógica de negócio do WhatsApp (login por número, cadastro, roteamento para ChatService).
/// Essa camada NÃO muda ao trocar de provider (Baileys → Meta Cloud).
/// </summary>
public interface IWhatsAppService
{
    /// <summary>
    /// Processa uma mensagem recebida via webhook.
    /// </summary>
    Task HandleIncomingAsync(JsonElement rawPayload);

    /// <summary>
    /// Vincula um número de WhatsApp a um usuário existente (via API autenticada).
    /// </summary>
    Task LinkPhoneNumberAsync(Guid userId, string phoneNumber);

    /// <summary>
    /// Desvincula o WhatsApp do usuário.
    /// </summary>
    Task UnlinkAsync(Guid userId);

    /// <summary>
    /// Retorna o status da vinculação.
    /// </summary>
    Task<WhatsAppStatusDto> GetStatusAsync(Guid userId);
}
