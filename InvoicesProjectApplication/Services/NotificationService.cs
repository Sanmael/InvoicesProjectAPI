using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvoicesProjectApplication.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationPreferenceRepository _preferenceRepository;
    private readonly IEmailNotificationRepository _notificationRepository;
    private readonly IFinancialSummaryService _financialSummaryService;
    private readonly ICreditCardRepository _creditCardRepository;
    private readonly ICardPurchaseRepository _cardPurchaseRepository;
    private readonly IDebtRepository _debtRepository;
    private readonly IReceivableRepository _receivableRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailSenderService _emailSenderService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationPreferenceRepository preferenceRepository,
        IEmailNotificationRepository notificationRepository,
        IFinancialSummaryService financialSummaryService,
        ICreditCardRepository creditCardRepository,
        ICardPurchaseRepository cardPurchaseRepository,
        IDebtRepository debtRepository,
        IReceivableRepository receivableRepository,
        IUserRepository userRepository,
        IEmailSenderService emailSenderService,
        ILogger<NotificationService> logger)
    {
        _preferenceRepository = preferenceRepository;
        _notificationRepository = notificationRepository;
        _financialSummaryService = financialSummaryService;
        _creditCardRepository = creditCardRepository;
        _cardPurchaseRepository = cardPurchaseRepository;
        _debtRepository = debtRepository;
        _receivableRepository = receivableRepository;
        _userRepository = userRepository;
        _emailSenderService = emailSenderService;
        _logger = logger;
    }

    public async Task ProcessAllNotificationsAsync()
    {
        var preferences = await _preferenceRepository.GetAllEnabledAsync();

        foreach (var pref in preferences)
        {
            try
            {
                await ProcessNotificationsForUserInternalAsync(pref);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar notificações para usuário {UserId}", pref.UserId);
            }
        }
    }

    public async Task ProcessNotificationsForUserAsync(Guid userId)
    {
        var preference = await _preferenceRepository.GetByUserIdAsync(userId);
        
        if (preference is null || !preference.EmailNotificationsEnabled)
        {
            _logger.LogInformation("Notificações desabilitadas para usuário {UserId}", userId);
            return;
        }

        await ProcessNotificationsForUserInternalAsync(preference);
    }

    private async Task ProcessNotificationsForUserInternalAsync(NotificationPreference preference)
    {
        var user = preference.User ?? await _userRepository.GetByIdAsync(preference.UserId);
        
        if (user is null || !user.IsActive)
            return;

        var now = DateTime.UtcNow;
        var monthKey = $"{now.Year}-{now.Month:D2}";

        // 1. Verificar saldo baixo
        if (preference.NotifyLowBalance)
        {
            await CheckLowBalanceAsync(user, preference, monthKey);
        }

        // 2. Verificar fechamento de fatura
        if (preference.NotifyInvoiceClosed)
        {
            await CheckInvoiceClosingAsync(user, preference, monthKey);
        }

        // 3. Verificar limite do cartão
        if (preference.NotifyCardLimitNearMax)
        {
            await CheckCardLimitAsync(user, preference, monthKey);
        }

        // 4. Verificar dívidas próximas
        if (preference.NotifyUpcomingDebts)
        {
            await CheckUpcomingDebtsAsync(user, preference);
        }

        // 5. Verificar recebíveis próximos
        if (preference.NotifyUpcomingReceivables)
        {
            await CheckUpcomingReceivablesAsync(user, preference);
        }
    }

    private async Task CheckLowBalanceAsync(User user, NotificationPreference preference, string monthKey)
    {
        var referenceKey = $"LowBalance_{monthKey}";
        
        if (await _notificationRepository.ExistsByReferenceKeyAsync(user.Id, referenceKey))
            return;

        var summary = await _financialSummaryService.GetCurrentMonthSummaryAsync(user.Id);
        
        if (summary.Balance < preference.LowBalanceThreshold)
        {
            var subject = "⚠️ Aviso: Saldo Baixo";
            var body = GenerateLowBalanceEmail(user.Name, summary.Balance, preference.LowBalanceThreshold);
            
            await SendAndLogNotificationAsync(user, NotificationType.LowBalance, subject, body, referenceKey);
        }
    }

    private async Task CheckInvoiceClosingAsync(User user, NotificationPreference preference, string monthKey)
    {
        var cards = await _creditCardRepository.GetActiveByUserIdAsync(user.Id);
        var today = DateTime.UtcNow.Date;

        foreach (var card in cards)
        {
            // Calcular data de fechamento deste mês
            var closingDate = new DateTime(today.Year, today.Month, 
                Math.Min(card.ClosingDay, DateTime.DaysInMonth(today.Year, today.Month)));

            var daysUntilClosing = (closingDate - today).Days;

            var invoiceClosedReferenceKey = $"InvoiceClosed_{card.Id}_{monthKey}";
            if (daysUntilClosing == 0)
            {
                if (await _notificationRepository.ExistsByReferenceKeyAsync(user.Id, invoiceClosedReferenceKey))
                    continue;

                var purchasesClosed = await _cardPurchaseRepository.GetByCardIdAndMonthAsync(
                    card.Id, today.Year, today.Month);
                var totalClosedInvoice = purchasesClosed.Where(p => !p.IsPaid).Sum(p => p.Amount / p.Installments);

                var closedSubject = $"🔒 Fatura do cartão {card.Name} fechou";
                var closedBody = GenerateInvoiceClosedEmail(user.Name, card.Name, card.LastFourDigits, totalClosedInvoice);

                await SendAndLogNotificationAsync(user, NotificationType.InvoiceClosed, closedSubject, closedBody,
                    invoiceClosedReferenceKey);
                continue;
            }

            var invoiceClosingReferenceKey = $"InvoiceClosing_{card.Id}_{monthKey}";
            if (await _notificationRepository.ExistsByReferenceKeyAsync(user.Id, invoiceClosingReferenceKey))
                continue;

            // Se está dentro do período de aviso (antes do fechamento)
            if (daysUntilClosing > 0 && daysUntilClosing <= preference.DaysBeforeInvoiceCloseNotification)
            {
                var purchases = await _cardPurchaseRepository.GetByCardIdAndMonthAsync(
                    card.Id, today.Year, today.Month);
                var totalInvoice = purchases.Where(p => !p.IsPaid).Sum(p => p.Amount / p.Installments);

                var subject = $"📅 Fatura do cartão {card.Name} fecha em {daysUntilClosing} dia(s)";
                var body = GenerateInvoiceClosingEmail(user.Name, card.Name, card.LastFourDigits, 
                    closingDate, totalInvoice, daysUntilClosing);
                
                await SendAndLogNotificationAsync(user, NotificationType.InvoiceClosing, subject, body, invoiceClosingReferenceKey);
            }
        }
    }

    private async Task CheckCardLimitAsync(User user, NotificationPreference preference, string monthKey)
    {
        var cards = await _creditCardRepository.GetActiveByUserIdAsync(user.Id);

        foreach (var card in cards)
        {
            var referenceKey = $"CardLimit_{card.Id}_{monthKey}";
            
            if (await _notificationRepository.ExistsByReferenceKeyAsync(user.Id, referenceKey))
                continue;

            var purchases = await _cardPurchaseRepository.GetByCardIdAsync(card.Id);
            var usedLimit = purchases.Where(p => !p.IsPaid).Sum(p => p.Amount);
            var usagePercentage = card.CreditLimit > 0 
                ? (usedLimit / card.CreditLimit) * 100 
                : 0;

            if (usagePercentage >= preference.CardLimitWarningPercentage)
            {
                var subject = $"💳 Limite do cartão {card.Name} acima de {preference.CardLimitWarningPercentage}%";
                var body = GenerateCardLimitEmail(user.Name, card.Name, card.LastFourDigits, 
                    usedLimit, card.CreditLimit ?? 0, usagePercentage ?? 0);
                
                await SendAndLogNotificationAsync(user, NotificationType.CardLimitNearMax, subject, body, referenceKey);
            }
        }
    }

    private async Task CheckUpcomingDebtsAsync(User user, NotificationPreference preference)
    {
        var debts = await _debtRepository.GetPendingByUserIdAsync(user.Id);
        var today = DateTime.UtcNow.Date;

        foreach (var debt in debts)
        {
            var referenceKey = $"Debt_{debt.Id}";
            
            if (await _notificationRepository.ExistsByReferenceKeyAsync(user.Id, referenceKey))
                continue;

            var daysUntilDue = (debt.DueDate.Date - today).Days;

            if (daysUntilDue >= 0 && daysUntilDue <= preference.DaysBeforeDebtDueNotification)
            {
                var subject = daysUntilDue == 0 
                    ? $"🔴 Dívida vence HOJE: {debt.Description}"
                    : $"⏰ Dívida vence em {daysUntilDue} dia(s): {debt.Description}";
                
                var body = GenerateDebtReminderEmail(user.Name, debt.Description, debt.Amount, 
                    debt.DueDate, daysUntilDue);
                
                await SendAndLogNotificationAsync(user, NotificationType.UpcomingDebt, subject, body, referenceKey);
            }
        }
    }

    private async Task CheckUpcomingReceivablesAsync(User user, NotificationPreference preference)
    {
        var receivables = await _receivableRepository.GetPendingByUserIdAsync(user.Id);
        var today = DateTime.UtcNow.Date;

        foreach (var receivable in receivables)
        {
            var referenceKey = $"Receivable_{receivable.Id}";
            
            if (await _notificationRepository.ExistsByReferenceKeyAsync(user.Id, referenceKey))
                continue;

            var daysUntilExpected = (receivable.ExpectedDate.Date - today).Days;

            if (daysUntilExpected >= 0 && daysUntilExpected <= preference.DaysBeforeReceivableNotification)
            {
                var subject = daysUntilExpected == 0 
                    ? $"💰 Recebível esperado para HOJE: {receivable.Description}"
                    : $"💵 Recebível em {daysUntilExpected} dia(s): {receivable.Description}";
                
                var body = GenerateReceivableReminderEmail(user.Name, receivable.Description, 
                    receivable.Amount, receivable.ExpectedDate, daysUntilExpected);
                
                await SendAndLogNotificationAsync(user, NotificationType.UpcomingReceivable, subject, body, referenceKey);
            }
        }
    }

    private async Task SendAndLogNotificationAsync(
        User user, 
        NotificationType type, 
        string subject, 
        string body, 
        string referenceKey)
    {
        var notification = new EmailNotification
        {
            UserId = user.Id,
            Type = type,
            Subject = subject,
            Body = body,
            RecipientEmail = user.Email,
            ReferenceKey = referenceKey,
            SentAt = DateTime.UtcNow
        };

        var result = await _emailSenderService.SendEmailAsync(user.Email, subject, body);
        
        notification.WasSent = result.Success;
        notification.ErrorMessage = result.ErrorMessage;

        await _notificationRepository.AddAsync(notification);

        if (result.Success)
        {
            _logger.LogInformation("Notificação {Type} enviada para {Email}", type, user.Email);
        }
        else
        {
            _logger.LogWarning("Falha ao enviar notificação {Type} para {Email}: {Error}", 
                type, user.Email, result.ErrorMessage);
        }
    }

    public async Task<IEnumerable<EmailNotificationDto>> GetNotificationHistoryAsync(Guid userId, int limit = 50)
    {
        var notifications = await _notificationRepository.GetByUserIdAsync(userId);
        
        return notifications
            .Take(limit)
            .Select(n => new EmailNotificationDto(
                n.Id,
                n.UserId,
                n.Type,
                n.Type.ToString(),
                n.Subject,
                n.RecipientEmail,
                n.SentAt,
                n.WasSent,
                n.ErrorMessage
            ));
    }

    public async Task<NotificationSummaryDto> GetNotificationSummaryAsync(Guid userId)
    {
        var notifications = await _notificationRepository.GetByUserIdAsync(userId);
        var notificationList = notifications.ToList();

        return new NotificationSummaryDto(
            TotalSent: notificationList.Count(n => n.WasSent),
            TotalFailed: notificationList.Count(n => !n.WasSent && n.ErrorMessage != null),
            TotalPending: notificationList.Count(n => !n.WasSent && n.ErrorMessage == null),
            LastSentAt: notificationList.Where(n => n.WasSent).MaxBy(n => n.SentAt)?.SentAt
        );
    }

    #region Email Templates

    private static string GenerateLowBalanceEmail(string userName, decimal balance, decimal threshold)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #dc3545; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background: #f9f9f9; }}
        .balance {{ font-size: 24px; color: #dc3545; font-weight: bold; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>⚠️ Aviso de Saldo Baixo</h1>
        </div>
        <div class='content'>
            <p>Olá, <strong>{userName}</strong>!</p>
            <p>Seu saldo mensal está abaixo do limite configurado.</p>
            <p>Saldo atual: <span class='balance'>R$ {balance:N2}</span></p>
            <p>Limite configurado: R$ {threshold:N2}</p>
            <p>Recomendamos revisar suas despesas e receitas para evitar ficar no negativo.</p>
        </div>
        <div class='footer'>
            <p>Sistema de Faturas - Notificação Automática</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string GenerateInvoiceClosingEmail(string userName, string cardName, string lastFour, 
        DateTime closingDate, decimal totalInvoice, int daysUntilClosing)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #ffc107; color: #333; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background: #f9f9f9; }}
        .amount {{ font-size: 24px; color: #333; font-weight: bold; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>📅 Fatura Fechando em Breve</h1>
        </div>
        <div class='content'>
            <p>Olá, <strong>{userName}</strong>!</p>
            <p>A fatura do seu cartão <strong>{cardName}</strong> (final {lastFour}) fechará em <strong>{daysUntilClosing}</strong> dia(s).</p>
            <p>Data de fechamento: <strong>{closingDate:dd/MM/yyyy}</strong></p>
            <p>Total atual da fatura: <span class='amount'>R$ {totalInvoice:N2}</span></p>
            <p>Compras realizadas após o fechamento entrarão na próxima fatura.</p>
        </div>
        <div class='footer'>
            <p>Sistema de Faturas - Notificação Automática</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string GenerateInvoiceClosedEmail(string userName, string cardName, string lastFour, decimal totalInvoice)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #28a745; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background: #f9f9f9; }}
        .amount {{ font-size: 24px; color: #28a745; font-weight: bold; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔒 Fatura Fechada</h1>
        </div>
        <div class='content'>
            <p>Olá, <strong>{userName}</strong>!</p>
            <p>A fatura do seu cartão <strong>{cardName}</strong> (final {lastFour}) foi fechada.</p>
            <p>Total da fatura: <span class='amount'>R$ {totalInvoice:N2}</span></p>
            <p>Lembre-se de pagar até a data de vencimento para evitar juros.</p>
        </div>
        <div class='footer'>
            <p>Sistema de Faturas - Notificação Automática</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string GenerateCardLimitEmail(string userName, string cardName, string lastFour, 
        decimal usedLimit, decimal totalLimit, decimal usagePercentage)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #fd7e14; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background: #f9f9f9; }}
        .percentage {{ font-size: 24px; color: #fd7e14; font-weight: bold; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>💳 Limite do Cartão Alto</h1>
        </div>
        <div class='content'>
            <p>Olá, <strong>{userName}</strong>!</p>
            <p>O uso do limite do seu cartão <strong>{cardName}</strong> (final {lastFour}) está alto.</p>
            <p>Uso atual: <span class='percentage'>{usagePercentage:N1}%</span></p>
            <p>Limite usado: R$ {usedLimit:N2} de R$ {totalLimit:N2}</p>
            <p>Limite disponível: R$ {(totalLimit - usedLimit):N2}</p>
        </div>
        <div class='footer'>
            <p>Sistema de Faturas - Notificação Automática</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string GenerateDebtReminderEmail(string userName, string description, decimal amount, 
        DateTime dueDate, int daysUntilDue)
    {
        var urgencyColor = daysUntilDue == 0 ? "#dc3545" : "#ffc107";
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: {urgencyColor}; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background: #f9f9f9; }}
        .amount {{ font-size: 24px; color: {urgencyColor}; font-weight: bold; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>{(daysUntilDue == 0 ? "🔴 Dívida Vence Hoje!" : "⏰ Lembrete de Dívida")}</h1>
        </div>
        <div class='content'>
            <p>Olá, <strong>{userName}</strong>!</p>
            <p>Você tem uma dívida {(daysUntilDue == 0 ? "que vence HOJE" : $"que vence em {daysUntilDue} dia(s)")}:</p>
            <p><strong>{description}</strong></p>
            <p>Valor: <span class='amount'>R$ {amount:N2}</span></p>
            <p>Vencimento: {dueDate:dd/MM/yyyy}</p>
        </div>
        <div class='footer'>
            <p>Sistema de Faturas - Notificação Automática</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string GenerateReceivableReminderEmail(string userName, string description, decimal amount, 
        DateTime expectedDate, int daysUntilExpected)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #28a745; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background: #f9f9f9; }}
        .amount {{ font-size: 24px; color: #28a745; font-weight: bold; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>{(daysUntilExpected == 0 ? "💰 Recebível Esperado Hoje!" : "💵 Lembrete de Recebível")}</h1>
        </div>
        <div class='content'>
            <p>Olá, <strong>{userName}</strong>!</p>
            <p>Você tem um valor a receber {(daysUntilExpected == 0 ? "esperado para HOJE" : $"esperado em {daysUntilExpected} dia(s)")}:</p>
            <p><strong>{description}</strong></p>
            <p>Valor: <span class='amount'>R$ {amount:N2}</span></p>
            <p>Data esperada: {expectedDate:dd/MM/yyyy}</p>
            <p>Lembre-se de confirmar o recebimento quando o valor for creditado.</p>
        </div>
        <div class='footer'>
            <p>Sistema de Faturas - Notificação Automática</p>
        </div>
    </div>
</body>
</html>";
    }

    #endregion
}
