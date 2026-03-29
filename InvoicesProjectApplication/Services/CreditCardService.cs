using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;

namespace InvoicesProjectApplication.Services;

public class CreditCardService : ICreditCardService
{
    private readonly ICreditCardRepository _creditCardRepository;
    private readonly ICardPurchaseRepository _cardPurchaseRepository;

    public CreditCardService(
        ICreditCardRepository creditCardRepository,
        ICardPurchaseRepository cardPurchaseRepository)
    {
        _creditCardRepository = creditCardRepository;
        _cardPurchaseRepository = cardPurchaseRepository;
    }

    private static DateTime ParseDateTimeUtc(string dateStr) =>
    DateTime.SpecifyKind(DateTime.Parse(dateStr), DateTimeKind.Utc);

    public async Task<CreditCardDto?> GetByIdAsync(Guid id)
    {
        var card = await _creditCardRepository.GetByIdAsync(id);
        if (card is null)
            return null;

        var totalPending = await _cardPurchaseRepository.GetTotalPendingByCardIdAsync(card.Id);
        return MapToDto(card, totalPending);
    }

    public async Task<CreditCardWithPurchasesDto?> GetWithPurchasesAsync(Guid id)
    {
        var card = await _creditCardRepository.GetWithPurchasesAsync(id);
        if (card is null) return null;

        var totalPending = await _cardPurchaseRepository.GetTotalPendingByCardIdAsync(id);
        var purchases = card.Purchases.Select(MapPurchaseToDto);

        return new CreditCardWithPurchasesDto(
            card.Id, card.Name, card.LastFourDigits, card.CreditLimit,
            card.ClosingDay, card.DueDay, card.IsActive, totalPending, purchases);
    }

    public async Task<IEnumerable<CreditCardDto>> GetByUserIdAsync(Guid userId)
    {
        var cards = await _creditCardRepository.GetByUserIdAsync(userId);
        var result = new List<CreditCardDto>();

        foreach (var card in cards)
        {
            var totalPending = await _cardPurchaseRepository.GetTotalPendingByCardIdAsync(card.Id);
            result.Add(MapToDto(card, totalPending));
        }

        return result;
    }

    public async Task<CreditCardDto> CreateAsync(Guid userId, CreateCreditCardDto dto)
    {
        var card = new CreditCard
        {
            UserId = userId,
            Name = dto.Name,
            LastFourDigits = dto.LastFourDigits,
            CreditLimit = dto.CreditLimit,
            ClosingDay = dto.ClosingDay,
            DueDay = dto.DueDay
        };

        await _creditCardRepository.AddAsync(card);
        return MapToDto(card, 0m);
    }

    public async Task<CreditCardDto> UpdateAsync(Guid id, UpdateCreditCardDto dto)
    {
        var card = await _creditCardRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Cartão não encontrado.");

        if (dto.Name is not null)
            card.Name = dto.Name;

        if (dto.CreditLimit.HasValue)
            card.CreditLimit = dto.CreditLimit.Value;

        if (dto.ClosingDay.HasValue)
            card.ClosingDay = dto.ClosingDay.Value;

        if (dto.DueDay.HasValue)
            card.DueDay = dto.DueDay.Value;

        if (dto.IsActive.HasValue)
            card.IsActive = dto.IsActive.Value;

        card.UpdatedAt = DateTime.UtcNow;
        await _creditCardRepository.UpdateAsync(card);
        var totalPending = await _cardPurchaseRepository.GetTotalPendingByCardIdAsync(card.Id);
        return MapToDto(card, totalPending);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _creditCardRepository.DeleteAsync(id);
    }

    public async Task<IEnumerable<BestCardRecommendationDto>> GetBestCardForTodayAsync(Guid userId)
    {
        var cards = await _creditCardRepository.GetActiveByUserIdAsync(userId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var recommendations = new List<BestCardRecommendationDto>();

        foreach (var card in cards)
        {
            // Determine next closing date
            DateOnly nextClosing;
            if (today.Day <= card.ClosingDay)
            {
                // Purchase still goes on current invoice
                nextClosing = new DateOnly(today.Year, today.Month, Math.Min(card.ClosingDay, DateTime.DaysInMonth(today.Year, today.Month)));
            }
            else
            {
                // Purchase goes on next month's invoice
                var nextMonth = today.AddMonths(1);
                nextClosing = new DateOnly(nextMonth.Year, nextMonth.Month, Math.Min(card.ClosingDay, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month)));
            }

            // Determine invoice due date (always after closing)
            DateOnly dueDate;
            if (card.DueDay > card.ClosingDay)
            {
                // Due date is in the same month as closing
                dueDate = new DateOnly(nextClosing.Year, nextClosing.Month, Math.Min(card.DueDay, DateTime.DaysInMonth(nextClosing.Year, nextClosing.Month)));
            }
            else
            {
                // Due date is in the month after closing
                var dueMonth = nextClosing.AddMonths(1);
                dueDate = new DateOnly(dueMonth.Year, dueMonth.Month, Math.Min(card.DueDay, DateTime.DaysInMonth(dueMonth.Year, dueMonth.Month)));
            }

            var daysUntilPayment = dueDate.DayNumber - today.DayNumber;

            var explanation = today.Day <= card.ClosingDay
                ? $"Compra entra na fatura atual (fecha dia {card.ClosingDay}), vence em {dueDate:dd/MM/yyyy} — {daysUntilPayment} dias de prazo."
                : $"Compra entra na próxima fatura (fecha dia {card.ClosingDay}), vence em {dueDate:dd/MM/yyyy} — {daysUntilPayment} dias de prazo.";

            recommendations.Add(new BestCardRecommendationDto(
                card.Id,
                card.Name,
                card.LastFourDigits,
                card.ClosingDay,
                card.DueDay,
                daysUntilPayment,
                nextClosing,
                dueDate,
                explanation));
        }

        return recommendations.OrderByDescending(r => r.DaysUntilPayment);
    }

    private static CreditCardDto MapToDto(CreditCard card, decimal totalPending)
    {
        var availableLimit = card.CreditLimit.HasValue ? card.CreditLimit.Value - totalPending : (decimal?)null;
        return new CreditCardDto(
            card.Id,
            card.Name,
            card.LastFourDigits,
            card.CreditLimit,
            card.ClosingDay,
            card.DueDay,
            card.IsActive,
            totalPending,
            availableLimit,
            card.CreatedAt);
    }

    private static CardPurchaseDto MapPurchaseToDto(CardPurchase purchase) =>
        new(purchase.Id, purchase.CreditCardId, purchase.Description, purchase.Amount,
            purchase.PurchaseDate, purchase.Installments, purchase.CurrentInstallment,
            purchase.IsPaid, purchase.Notes, purchase.Category, purchase.CreatedAt);
}
