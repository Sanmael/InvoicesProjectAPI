using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;

namespace InvoicesProjectApplication.Services;

public class CardPurchaseService : ICardPurchaseService
{
    private readonly ICardPurchaseRepository _cardPurchaseRepository;
    private readonly ICreditCardRepository _creditCardRepository;

    public CardPurchaseService(
        ICardPurchaseRepository cardPurchaseRepository,
        ICreditCardRepository creditCardRepository)
    {
        _cardPurchaseRepository = cardPurchaseRepository;
        _creditCardRepository = creditCardRepository;
    }

    public async Task<CardPurchaseDto?> GetByIdAsync(Guid id)
    {
        var purchase = await _cardPurchaseRepository.GetByIdAsync(id);
        return purchase is null ? null : MapToDto(purchase);
    }

    public async Task<IEnumerable<CardPurchaseDto>> GetByCreditCardIdAsync(Guid creditCardId)
    {
        var purchases = await _cardPurchaseRepository.GetByCreditCardIdAsync(creditCardId);
        return purchases.Select(MapToDto);
    }

    public async Task<IEnumerable<CardPurchaseDto>> GetPendingByCreditCardIdAsync(Guid creditCardId)
    {
        var purchases = await _cardPurchaseRepository.GetPendingByCreditCardIdAsync(creditCardId);
        return purchases.Select(MapToDto);
    }

    public async Task<CardPurchaseDto> CreateAsync(CreateCardPurchaseDto dto)
    {
        var card = await _creditCardRepository.GetByIdAsync(dto.CreditCardId)
            ?? throw new KeyNotFoundException("Cartão não encontrado.");

        var purchase = new CardPurchase
        {
            CreditCardId = dto.CreditCardId,
            Description = dto.Description,
            Amount = dto.Amount,
            PurchaseDate = dto.PurchaseDate,
            Installments = dto.Installments,
            CurrentInstallment = 1,
            Notes = dto.Notes
        };

        await _cardPurchaseRepository.AddAsync(purchase);
        return MapToDto(purchase);
    }

    public async Task<CardPurchaseDto> UpdateAsync(Guid id, UpdateCardPurchaseDto dto)
    {
        var purchase = await _cardPurchaseRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Compra não encontrada.");

        if (dto.Description is not null)
            purchase.Description = dto.Description;

        if (dto.Amount.HasValue)
            purchase.Amount = dto.Amount.Value;

        if (dto.PurchaseDate.HasValue)
            purchase.PurchaseDate = dto.PurchaseDate.Value;

        if (dto.Installments.HasValue)
            purchase.Installments = dto.Installments.Value;

        if (dto.IsPaid.HasValue)
            purchase.IsPaid = dto.IsPaid.Value;

        if (dto.Notes is not null)
            purchase.Notes = dto.Notes;

        purchase.UpdatedAt = DateTime.UtcNow;
        await _cardPurchaseRepository.UpdateAsync(purchase);
        return MapToDto(purchase);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _cardPurchaseRepository.DeleteAsync(id);
    }

    public async Task<CardPurchaseDto> MarkAsPaidAsync(Guid id)
    {
        var purchase = await _cardPurchaseRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Compra não encontrada.");

        purchase.IsPaid = true;
        purchase.UpdatedAt = DateTime.UtcNow;

        await _cardPurchaseRepository.UpdateAsync(purchase);
        return MapToDto(purchase);
    }

    private static CardPurchaseDto MapToDto(CardPurchase purchase) =>
        new(purchase.Id, purchase.CreditCardId, purchase.Description, purchase.Amount,
            purchase.PurchaseDate, purchase.Installments, purchase.CurrentInstallment,
            purchase.IsPaid, purchase.Notes, purchase.CreatedAt);
}
