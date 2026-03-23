using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

public interface ICardPurchaseService
{
    Task<CardPurchaseDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<CardPurchaseDto>> GetByCreditCardIdAsync(Guid creditCardId);
    Task<IEnumerable<CardPurchaseDto>> GetPendingByCreditCardIdAsync(Guid creditCardId);
    Task<CardPurchaseDto> CreateAsync(CreateCardPurchaseDto dto);
    Task<CardPurchaseDto> UpdateAsync(Guid id, UpdateCardPurchaseDto dto);
    Task DeleteAsync(Guid id);
    Task<CardPurchaseDto> MarkAsPaidAsync(Guid id);
}
