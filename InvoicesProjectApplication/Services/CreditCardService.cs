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
