using InvoicesProjectEntities.Entities;

namespace InvoicesProjectApplication.DTOs;

/// <summary>
/// DTO para exibição das preferências de notificação
/// </summary>
public record NotificationPreferenceDto(
    Guid Id,
    Guid UserId,
    bool NotifyLowBalance,
    decimal LowBalanceThreshold,
    bool NotifyInvoiceClosed,
    int DaysBeforeInvoiceCloseNotification,
    bool NotifyCardLimitNearMax,
    int CardLimitWarningPercentage,
    bool NotifyUpcomingDebts,
    int DaysBeforeDebtDueNotification,
    bool NotifyUpcomingReceivables,
    int DaysBeforeReceivableNotification,
    bool EmailNotificationsEnabled
);

/// <summary>
/// DTO para criar/atualizar preferências de notificação
/// </summary>
public record NotificationPreferenceCreateDto(
    bool NotifyLowBalance,
    decimal LowBalanceThreshold,
    bool NotifyInvoiceClosed,
    int DaysBeforeInvoiceCloseNotification,
    bool NotifyCardLimitNearMax,
    int CardLimitWarningPercentage,
    bool NotifyUpcomingDebts,
    int DaysBeforeDebtDueNotification,
    bool NotifyUpcomingReceivables,
    int DaysBeforeReceivableNotification,
    bool EmailNotificationsEnabled
);

/// <summary>
/// DTO para histórico de notificações enviadas
/// </summary>
public record EmailNotificationDto(
    Guid Id,
    Guid UserId,
    NotificationType Type,
    string TypeName,
    string Subject,
    string RecipientEmail,
    DateTime SentAt,
    bool WasSent,
    string? ErrorMessage
);

/// <summary>
/// DTO para resumo de notificações
/// </summary>
public record NotificationSummaryDto(
    int TotalSent,
    int TotalFailed,
    int TotalPending,
    DateTime? LastSentAt
);
