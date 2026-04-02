using System.Collections.Concurrent;
using System.Text.Json;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvoicesProjectApplication.Services;

public class WhatsAppService : IWhatsAppService
{
    private readonly IWhatsAppProvider _provider;
    private readonly IUserRepository _userRepository;
    private readonly IChatService _chatService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<WhatsAppService> _logger;

    // Estado das conversas de cadastro/login em andamento (por número de telefone)
    private static readonly ConcurrentDictionary<string, ConversationState> _conversations = new();

    // Histórico de chat por número de telefone (simula o que o frontend web faz)
    private static readonly ConcurrentDictionary<string, List<ChatMessageDto>> _chatHistories = new();

    public WhatsAppService(
        IWhatsAppProvider provider,
        IUserRepository userRepository,
        IChatService chatService,
        IPasswordHasher passwordHasher,
        ILogger<WhatsAppService> logger)
    {
        _provider = provider;
        _userRepository = userRepository;
        _chatService = chatService;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task HandleIncomingAsync(JsonElement rawPayload)
    {
        var msg = _provider.ParseWebhook(rawPayload);
        if (msg is null) return;

        _logger.LogInformation("WhatsApp msg de {Phone}: {Text}", msg.PhoneNumber, msg.Text);

        // Verifica se há conversa de cadastro/login em andamento
        if (_conversations.TryGetValue(msg.PhoneNumber, out var state))
        {
            await HandleConversationFlow(msg, state);
            return;
        }

        var user = await _userRepository.GetByPhoneNumberAsync(msg.PhoneNumber);

        if (user is null)
        {
            await HandleUnknownUser(msg);
            return;
        }

        // Usuário existe e está vinculado → roteia para o ChatService (IA)
        try
        {
            var history = _chatHistories.GetOrAdd(msg.PhoneNumber, _ => new List<ChatMessageDto>());

            List<ChatMessageDto> historySnapshot;
            lock (history)
            {
                historySnapshot = history.ToList();
            }

            var chatRequest = new ChatRequestDto(msg.Text, historySnapshot);
            var response = await _chatService.ProcessMessageAsync(user.Id, chatRequest);

            lock (history)
            {
                history.Add(new ChatMessageDto("user", msg.Text));
                history.Add(new ChatMessageDto("assistant", response.Reply));

                // Mantém no máximo 30 mensagens (igual ao frontend web)
                if (history.Count > 30)
                    history.RemoveRange(0, history.Count - 30);
            }

            await _provider.SendMessageAsync(msg.PhoneNumber, response.Reply);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar mensagem WhatsApp de {Phone}", msg.PhoneNumber);
            await _provider.SendMessageAsync(msg.PhoneNumber,
                "❌ Ocorreu um erro ao processar sua mensagem. Tente novamente.");
        }
    }

    public async Task LinkPhoneNumberAsync(Guid userId, string phoneNumber)
    {
        var normalized = NormalizePhoneNumber(phoneNumber);

        // Verificar se o número já está em uso por outro usuário
        var existing = await _userRepository.GetByPhoneNumberAsync(normalized);
        if (existing is not null && existing.Id != userId)
            throw new InvalidOperationException("Este número de WhatsApp já está vinculado a outra conta.");

        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");

        user.WhatsAppPhoneNumber = normalized;
        user.WhatsAppLinked = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
    }

    public async Task UnlinkAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");

        user.WhatsAppPhoneNumber = null;
        user.WhatsAppLinked = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
    }

    public async Task<WhatsAppStatusDto> GetStatusAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
            return new WhatsAppStatusDto(false, null);

        return new WhatsAppStatusDto(user.WhatsAppLinked, user.WhatsAppPhoneNumber);
    }

    private async Task HandleUnknownUser(WhatsAppIncomingMessage msg)
    {
        var text = msg.Text.Trim();

        if (text == "1")
        {
            _conversations[msg.PhoneNumber] = new ConversationState { Step = ConversationStep.RegisterName };
            await _provider.SendMessageAsync(msg.PhoneNumber,
                "📝 *Cadastro de nova conta*\n\nDigite seu *nome completo*:");
            return;
        }

        if (text == "2")
        {
            _conversations[msg.PhoneNumber] = new ConversationState { Step = ConversationStep.LoginEmail };
            await _provider.SendMessageAsync(msg.PhoneNumber,
                "🔐 *Vincular conta existente*\n\nDigite o *e-mail* da sua conta:");
            return;
        }

        await _provider.SendMessageAsync(msg.PhoneNumber,
            $"👋 Olá{(msg.SenderName != null ? $", *{msg.SenderName}*" : "")}! " +
            "Seu número ainda não está vinculado ao sistema.\n\n" +
            "O que deseja fazer?\n\n" +
            "*1* — Criar uma nova conta\n" +
            "*2* — Vincular a uma conta existente\n\n" +
            "Responda com *1* ou *2*.");
    }

    private async Task HandleConversationFlow(WhatsAppIncomingMessage msg, ConversationState state)
    {
        var text = msg.Text.Trim();

        // Permite cancelar a qualquer momento
        if (text.Equals("cancelar", StringComparison.OrdinalIgnoreCase) || text == "0")
        {
            _conversations.TryRemove(msg.PhoneNumber, out _);
            await _provider.SendMessageAsync(msg.PhoneNumber, "❌ Operação cancelada.");
            return;
        }

        switch (state.Step)
        {
            case ConversationStep.RegisterName:
                if (text.Length < 2)
                {
                    await _provider.SendMessageAsync(msg.PhoneNumber, "Nome muito curto. Digite seu *nome completo*:");
                    return;
                }
                state.Name = text;
                state.Step = ConversationStep.RegisterEmail;
                await _provider.SendMessageAsync(msg.PhoneNumber, "📧 Agora digite seu *e-mail*:");
                break;

            case ConversationStep.RegisterEmail:
                if (!text.Contains('@') || !text.Contains('.'))
                {
                    await _provider.SendMessageAsync(msg.PhoneNumber, "E-mail inválido. Digite um *e-mail* válido:");
                    return;
                }
                var existingByEmail = await _userRepository.GetByEmailAsync(text.ToLowerInvariant());
                if (existingByEmail is not null)
                {
                    await _provider.SendMessageAsync(msg.PhoneNumber,
                        "⚠️ Já existe uma conta com este e-mail.\n\n" +
                        "Deseja vincular este número a ela?\n" +
                        "Se sim, digite a *senha* da conta.\n" +
                        "Ou envie *cancelar* para voltar.");
                    state.Email = text.ToLowerInvariant();
                    state.Step = ConversationStep.LoginPassword;
                    return;
                }
                state.Email = text.ToLowerInvariant();
                state.Step = ConversationStep.RegisterPassword;
                await _provider.SendMessageAsync(msg.PhoneNumber,
                    "🔒 Crie uma *senha* (mínimo 6 caracteres):");
                break;

            case ConversationStep.RegisterPassword:
                if (text.Length < 6)
                {
                    await _provider.SendMessageAsync(msg.PhoneNumber, "Senha muito curta. Mínimo *6 caracteres*:");
                    return;
                }
                state.Password = text;
                state.Step = ConversationStep.RegisterConfirm;
                await _provider.SendMessageAsync(msg.PhoneNumber,
                    $"✅ Confirme seus dados:\n\n" +
                    $"👤 Nome: *{state.Name}*\n" +
                    $"📧 E-mail: *{state.Email}*\n\n" +
                    "Está correto? Responda *sim* ou *não*.");
                break;

            case ConversationStep.RegisterConfirm:
                if (text.Equals("sim", StringComparison.OrdinalIgnoreCase) || text.Equals("s", StringComparison.OrdinalIgnoreCase))
                {
                    await CompleteRegistration(msg.PhoneNumber, state);
                }
                else
                {
                    _conversations.TryRemove(msg.PhoneNumber, out _);
                    await _provider.SendMessageAsync(msg.PhoneNumber,
                        "❌ Cadastro cancelado. Envie qualquer mensagem para recomeçar.");
                }
                break;

            case ConversationStep.LoginEmail:
                if (!text.Contains('@') || !text.Contains('.'))
                {
                    await _provider.SendMessageAsync(msg.PhoneNumber, "E-mail inválido. Digite o *e-mail* da sua conta:");
                    return;
                }
                state.Email = text.ToLowerInvariant();
                state.Step = ConversationStep.LoginPassword;
                await _provider.SendMessageAsync(msg.PhoneNumber, "🔒 Agora digite sua *senha*:");
                break;

            case ConversationStep.LoginPassword:
                state.Password = text;
                await CompleteLogin(msg.PhoneNumber, state);
                break;
        }
    }

    private async Task CompleteRegistration(string phoneNumber, ConversationState state)
    {
        try
        {
            var user = new User
            {
                Name = state.Name!,
                Email = state.Email!,
                PasswordHash = _passwordHasher.Hash(state.Password!),
                WhatsAppPhoneNumber = phoneNumber,
                WhatsAppLinked = true,
                IsActive = true
            };

            await _userRepository.AddAsync(user);
            _conversations.TryRemove(phoneNumber, out _);

            await _provider.SendMessageAsync(phoneNumber,
                "🎉 Conta criada com sucesso!\n\n" +
                $"👤 {user.Name}\n" +
                $"📧 {user.Email}\n" +
                $"📱 WhatsApp vinculado\n\n" +
                "Agora você pode usar o sistema por aqui! Envie qualquer pergunta sobre suas finanças.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registrar usuário via WhatsApp {Phone}", phoneNumber);
            _conversations.TryRemove(phoneNumber, out _);
            await _provider.SendMessageAsync(phoneNumber,
                "❌ Erro ao criar conta. Tente novamente mais tarde.");
        }
    }

    private async Task CompleteLogin(string phoneNumber, ConversationState state)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(state.Email!);
            if (user is null || !_passwordHasher.Verify(state.Password!, user.PasswordHash))
            {
                _conversations.TryRemove(phoneNumber, out _);
                await _provider.SendMessageAsync(phoneNumber,
                    "❌ E-mail ou senha incorretos.\n\nEnvie qualquer mensagem para tentar novamente.");
                return;
            }

            // Verificar se o número já está vinculado a outro usuário
            var existingPhone = await _userRepository.GetByPhoneNumberAsync(phoneNumber);
            if (existingPhone is not null && existingPhone.Id != user.Id)
            {
                _conversations.TryRemove(phoneNumber, out _);
                await _provider.SendMessageAsync(phoneNumber,
                    "⚠️ Este número já está vinculado a outra conta.");
                return;
            }

            user.WhatsAppPhoneNumber = phoneNumber;
            user.WhatsAppLinked = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
            _conversations.TryRemove(phoneNumber, out _);

            await _provider.SendMessageAsync(phoneNumber,
                $"✅ WhatsApp vinculado com sucesso!\n\n" +
                $"👤 {user.Name}\n" +
                $"📧 {user.Email}\n\n" +
                "Agora você pode usar o sistema por aqui! Envie qualquer pergunta sobre suas finanças.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao vincular WhatsApp {Phone}", phoneNumber);
            _conversations.TryRemove(phoneNumber, out _);
            await _provider.SendMessageAsync(phoneNumber,
                "❌ Erro ao vincular conta. Tente novamente mais tarde.");
        }
    }

    private static string NormalizePhoneNumber(string phoneNumber)
    {
        // Remove tudo que não é dígito
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

        if (string.IsNullOrEmpty(digits) || digits.Length < 10)
            throw new InvalidOperationException("Número de telefone inválido. Use formato internacional (ex: 5511999998888).");

        return digits;
    }

    private enum ConversationStep
    {
        RegisterName,
        RegisterEmail,
        RegisterPassword,
        RegisterConfirm,
        LoginEmail,
        LoginPassword
    }

    private class ConversationState
    {
        public ConversationStep Step { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
    }
}
