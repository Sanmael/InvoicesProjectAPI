using System.Net.Http.Json;
using System.Text.Json;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InvoicesProjectInfra.Services;

/// <summary>
/// Provider que se comunica com o serviço Node.js Baileys via HTTP.
/// Para trocar para Meta Cloud API, basta criar MetaCloudWhatsAppProvider
/// e trocar o registro no DI.
/// </summary>
public class BaileysWhatsAppProvider : IWhatsAppProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BaileysWhatsAppProvider> _logger;
    private readonly string _baileysBaseUrl;

    public BaileysWhatsAppProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<BaileysWhatsAppProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _baileysBaseUrl = configuration["WhatsApp:BaileysBaseUrl"] ?? "http://localhost:3001";
    }

    public async Task SendMessageAsync(string phoneNumber, string message)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var jid = $"{phoneNumber}@c.us";

            var response = await client.PostAsJsonAsync(
                $"{_baileysBaseUrl}/send",
                new { jid, message });

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Baileys API error: {Status} {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar mensagem WhatsApp para {Phone}", phoneNumber);
        }
    }

    public WhatsAppIncomingMessage? ParseWebhook(JsonElement payload)
    {
        try
        {
            // Formato esperado do serviço whatsapp-web.js:
            // { "from": "5511999998888@c.us", "message": { "conversation": "texto" }, "pushName": "Nome" }

            if (!payload.TryGetProperty("from", out var fromProp))
                return null;

            var from = fromProp.GetString();
            if (string.IsNullOrEmpty(from))
                return null;

            // Remove @s.whatsapp.net e @c.us
            var phoneNumber = from.Split('@')[0];

            string? text = null;

            if (payload.TryGetProperty("message", out var messageProp))
            {
                if (messageProp.TryGetProperty("conversation", out var convProp))
                    text = convProp.GetString();
                else if (messageProp.TryGetProperty("extendedTextMessage", out var extProp)
                         && extProp.TryGetProperty("text", out var extTextProp))
                    text = extTextProp.GetString();
            }

            if (string.IsNullOrWhiteSpace(text))
                return null;

            string? pushName = null;
            if (payload.TryGetProperty("pushName", out var nameProp))
                pushName = nameProp.GetString();

            return new WhatsAppIncomingMessage(phoneNumber, text.Trim(), pushName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao parsear webhook Baileys");
            return null;
        }
    }
}
