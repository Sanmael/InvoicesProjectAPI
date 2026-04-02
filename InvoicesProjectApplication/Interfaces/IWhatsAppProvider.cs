using System.Text.Json;
using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

/// <summary>
/// Abstração do transporte WhatsApp.
/// Implementações: BaileysWhatsAppProvider (hoje), MetaCloudWhatsAppProvider (futuro).
/// Trocar de provider = trocar apenas a implementação no DI.
/// </summary>
public interface IWhatsAppProvider
{
    /// <summary>
    /// Envia uma mensagem de texto para o número informado.
    /// </summary>
    Task SendMessageAsync(string phoneNumber, string message);

    /// <summary>
    /// Parseia o payload bruto do webhook recebido do provider.
    /// Retorna null se não for uma mensagem de texto válida.
    /// </summary>
    WhatsAppIncomingMessage? ParseWebhook(JsonElement payload);
}
