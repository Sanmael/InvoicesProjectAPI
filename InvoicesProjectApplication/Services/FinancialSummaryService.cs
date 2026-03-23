using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Interfaces;

namespace InvoicesProjectApplication.Services;

public class FinancialSummaryService : IFinancialSummaryService
{
    private readonly IDebtRepository _debtRepository;
    private readonly IReceivableRepository _receivableRepository;
    private readonly ICardPurchaseRepository _cardPurchaseRepository;

    public FinancialSummaryService(
        IDebtRepository debtRepository,
        IReceivableRepository receivableRepository,
        ICardPurchaseRepository cardPurchaseRepository)
    {
        _debtRepository = debtRepository;
        _receivableRepository = receivableRepository;
        _cardPurchaseRepository = cardPurchaseRepository;
    }

    public async Task<FinancialSummaryDto> GetMonthlySummaryAsync(Guid userId, int year, int month)
    {
        var debts = await _debtRepository.GetByUserIdAndMonthAsync(userId, year, month);
        var receivables = await _receivableRepository.GetByUserIdAndMonthAsync(userId, year, month);
        var cardPurchases = await _cardPurchaseRepository.GetByUserIdAndMonthAsync(userId, year, month);

        var totalDebts = debts.Where(d => !d.IsPaid).Sum(d => d.Amount);
        var totalReceivables = receivables.Where(r => !r.IsReceived).Sum(r => r.Amount);
        var totalCardPurchases = cardPurchases.Where(p => !p.IsPaid).Sum(p => p.Amount / p.Installments);

        var totalToPay = totalDebts + totalCardPurchases;
        var balance = totalReceivables - totalToPay;

        return new FinancialSummaryDto(
            year,
            month,
            totalDebts,
            totalReceivables,
            totalCardPurchases,
            totalToPay,
            balance
        );
    }

    public async Task<FinancialSummaryDto> GetCurrentMonthSummaryAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        return await GetMonthlySummaryAsync(userId, now.Year, now.Month);
    }
}
