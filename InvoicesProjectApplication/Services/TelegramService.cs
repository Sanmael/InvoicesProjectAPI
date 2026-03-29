using System.Net.Http.Json;
using System.Security.Cryptography;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InvoicesProjectApplication.Services;

public class TelegramService : ITelegramService
{
    private readonly INotificationPreferenceRepository _prefRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TelegramService> _logger;
    private readonly string _botToken;
    private readonly string _botUsername;

    public TelegramService(
        INotificationPreferenceRepository prefRepository,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TelegramService> logger)
    {
        _prefRepository = prefRepository;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _botToken = configuration["Telegram:BotToken"] ?? "";
        _botUsername = configuration["Telegram:BotUsername"] ?? "";
    }

    public async Task<TelegramLinkDto> GenerateLinkAsync(Guid userId)
    {
        var pref = await _prefRepository.GetByUserIdAsync(userId);
        if (pref is null)
            throw new InvalidOperationException("Preferências de notificação não encontradas.");

        // Gerar token único para vincular
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        pref.TelegramLinkToken = token;
        pref.UpdatedAt = DateTime.UtcNow;
        await _prefRepository.UpdateAsync(pref);

        return new TelegramLinkDto(_botUsername, token);
    }

    public async Task<TelegramStatusDto> GetStatusAsync(Guid userId)
    {
        var pref = await _prefRepository.GetByUserIdAsync(userId);
        if (pref is null)
            return new TelegramStatusDto(false, null, false);

        return new TelegramStatusDto(
            pref.TelegramChatId.HasValue,
            pref.TelegramUsername,
            pref.TelegramNotificationsEnabled
        );
    }

    public async Task ToggleNotificationsAsync(Guid userId, bool enabled)
    {
        var pref = await _prefRepository.GetByUserIdAsync(userId);
        if (pref is null)
            throw new InvalidOperationException("Preferências de notificação não encontradas.");

        if (enabled && !pref.TelegramChatId.HasValue)
            throw new InvalidOperationException("Telegram não está vinculado. Vincule primeiro.");

        pref.TelegramNotificationsEnabled = enabled;
        pref.UpdatedAt = DateTime.UtcNow;
        await _prefRepository.UpdateAsync(pref);
    }

    public async Task UnlinkAsync(Guid userId)
    {
        var pref = await _prefRepository.GetByUserIdAsync(userId);
        if (pref is null) return;

        pref.TelegramChatId = null;
        pref.TelegramUsername = null;
        pref.TelegramLinkToken = null;
        pref.TelegramNotificationsEnabled = false;
        pref.UpdatedAt = DateTime.UtcNow;
        await _prefRepository.UpdateAsync(pref);
    }

    public async Task HandleWebhookAsync(TelegramWebhookUpdateDto update)
    {
        var message = update.Message;
        if (message?.Text is null || message.Chat is null) return;

        var chatId = message.Chat.Id;
        var text = message.Text.Trim();

        if (text.StartsWith("/start"))
        {
            var parts = text.Split(' ', 2);
            if (parts.Length < 2)
            {
                await SendMessageAsync(chatId,
                    "👋 Olá! Para vincular sua conta, use o link gerado no sistema.\n\n" +
                    "Acesse seu perfil no sistema e clique em 'Vincular Telegram'.");
                return;
            }

            var token = parts[1].Trim();
            var pref = await _prefRepository.GetByLinkTokenAsync(token);

            if (pref is null)
            {
                await SendMessageAsync(chatId,
                    "❌ Token inválido ou expirado. Gere um novo link no sistema.");
                return;
            }

            pref.TelegramChatId = chatId;
            pref.TelegramUsername = message.From?.Username;
            pref.TelegramLinkToken = null; // Token usado
            pref.TelegramNotificationsEnabled = true;
            pref.UpdatedAt = DateTime.UtcNow;
            await _prefRepository.UpdateAsync(pref);

            await SendMessageAsync(chatId,
                "✅ Telegram vinculado com sucesso! Você receberá notificações financeiras aqui.\n\n" +
                "Comandos disponíveis:\n" +
                "/status - Ver status da vinculação\n" +
                "/desvincular - Desvincular conta");
        }
        else if (text == "/status")
        {
            var pref = await _prefRepository.GetByChatIdAsync(chatId);
            if (pref is null)
            {
                await SendMessageAsync(chatId, "❌ Nenhuma conta vinculada. Use o link do sistema para vincular.");
                return;
            }

            await SendMessageAsync(chatId,
                $"✅ Conta vinculada\n" +
                $"📱 Notificações: {(pref.TelegramNotificationsEnabled ? "Ativas" : "Desativadas")}");
        }
        else if (text == "/desvincular")
        {
            var pref = await _prefRepository.GetByChatIdAsync(chatId);
            if (pref is null)
            {
                await SendMessageAsync(chatId, "Nenhuma conta vinculada.");
                return;
            }

            pref.TelegramChatId = null;
            pref.TelegramUsername = null;
            pref.TelegramNotificationsEnabled = false;
            pref.UpdatedAt = DateTime.UtcNow;
            await _prefRepository.UpdateAsync(pref);

            await SendMessageAsync(chatId, "✅ Conta desvinculada. Você não receberá mais notificações.");
        }
        else
        {
            await SendMessageAsync(chatId,
                "🤖 Sou o bot de notificações do seu sistema financeiro.\n\n" +
                "Comandos:\n" +
                "/status - Ver status\n" +
                "/desvincular - Desvincular conta");
        }
    }

    public async Task SendMessageAsync(long chatId, string message)
    {
        if (string.IsNullOrEmpty(_botToken))
        {
            _logger.LogWarning("Telegram BotToken não configurado. Mensagem não enviada.");
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";

            var payload = new { chat_id = chatId, text = message, parse_mode = "HTML" };
            var response = await client.PostAsJsonAsync(url, payload);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Telegram API error: {Status} {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar mensagem Telegram para chatId {ChatId}", chatId);
        }
    }
}
