using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;

namespace InvoicesProjectApplication.Services;

public class NotificationPreferenceService : INotificationPreferenceService
{
    private readonly INotificationPreferenceRepository _repository;

    public NotificationPreferenceService(INotificationPreferenceRepository repository)
    {
        _repository = repository;
    }

    public async Task<NotificationPreferenceDto?> GetByUserIdAsync(Guid userId)
    {
        var preference = await _repository.GetByUserIdAsync(userId);
        
        if (preference is null)
            return null;

        return MapToDto(preference);
    }

    public async Task<NotificationPreferenceDto> SaveAsync(Guid userId, NotificationPreferenceCreateDto dto)
    {
        var existing = await _repository.GetByUserIdAsync(userId);

        if (existing is null)
        {
            var newPreference = new NotificationPreference
            {
                UserId = userId,
                NotifyLowBalance = dto.NotifyLowBalance,
                LowBalanceThreshold = dto.LowBalanceThreshold,
                NotifyInvoiceClosed = dto.NotifyInvoiceClosed,
                DaysBeforeInvoiceCloseNotification = dto.DaysBeforeInvoiceCloseNotification,
                NotifyCardLimitNearMax = dto.NotifyCardLimitNearMax,
                CardLimitWarningPercentage = dto.CardLimitWarningPercentage,
                NotifyUpcomingDebts = dto.NotifyUpcomingDebts,
                DaysBeforeDebtDueNotification = dto.DaysBeforeDebtDueNotification,
                NotifyUpcomingReceivables = dto.NotifyUpcomingReceivables,
                DaysBeforeReceivableNotification = dto.DaysBeforeReceivableNotification,
                EmailNotificationsEnabled = dto.EmailNotificationsEnabled
            };

            var saved = await _repository.AddAsync(newPreference);
            return MapToDto(saved);
        }

        existing.NotifyLowBalance = dto.NotifyLowBalance;
        existing.LowBalanceThreshold = dto.LowBalanceThreshold;
        existing.NotifyInvoiceClosed = dto.NotifyInvoiceClosed;
        existing.DaysBeforeInvoiceCloseNotification = dto.DaysBeforeInvoiceCloseNotification;
        existing.NotifyCardLimitNearMax = dto.NotifyCardLimitNearMax;
        existing.CardLimitWarningPercentage = dto.CardLimitWarningPercentage;
        existing.NotifyUpcomingDebts = dto.NotifyUpcomingDebts;
        existing.DaysBeforeDebtDueNotification = dto.DaysBeforeDebtDueNotification;
        existing.NotifyUpcomingReceivables = dto.NotifyUpcomingReceivables;
        existing.DaysBeforeReceivableNotification = dto.DaysBeforeReceivableNotification;
        existing.EmailNotificationsEnabled = dto.EmailNotificationsEnabled;
        existing.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(existing);
        return MapToDto(updated);
    }

    public NotificationPreferenceDto GetDefaults()
    {
        return new NotificationPreferenceDto(
            Id: Guid.Empty,
            UserId: Guid.Empty,
            NotifyLowBalance: true,
            LowBalanceThreshold: 500,
            NotifyInvoiceClosed: true,
            DaysBeforeInvoiceCloseNotification: 3,
            NotifyCardLimitNearMax: true,
            CardLimitWarningPercentage: 80,
            NotifyUpcomingDebts: true,
            DaysBeforeDebtDueNotification: 5,
            NotifyUpcomingReceivables: true,
            DaysBeforeReceivableNotification: 3,
            EmailNotificationsEnabled: true
        );
    }

    private static NotificationPreferenceDto MapToDto(NotificationPreference preference)
    {
        return new NotificationPreferenceDto(
            preference.Id,
            preference.UserId,
            preference.NotifyLowBalance,
            preference.LowBalanceThreshold,
            preference.NotifyInvoiceClosed,
            preference.DaysBeforeInvoiceCloseNotification,
            preference.NotifyCardLimitNearMax,
            preference.CardLimitWarningPercentage,
            preference.NotifyUpcomingDebts,
            preference.DaysBeforeDebtDueNotification,
            preference.NotifyUpcomingReceivables,
            preference.DaysBeforeReceivableNotification,
            preference.EmailNotificationsEnabled
        );
    }
}
