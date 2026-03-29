using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

public interface ICreditCardService
{
    Task<CreditCardDto?> GetByIdAsync(Guid id);
    Task<CreditCardWithPurchasesDto?> GetWithPurchasesAsync(Guid id);
    Task<IEnumerable<CreditCardDto>> GetByUserIdAsync(Guid userId);
    Task<CreditCardDto> CreateAsync(Guid userId, CreateCreditCardDto dto);
    Task<CreditCardDto> UpdateAsync(Guid id, UpdateCreditCardDto dto);
    Task DeleteAsync(Guid id);
    Task<IEnumerable<BestCardRecommendationDto>> GetBestCardForTodayAsync(Guid userId);
}
