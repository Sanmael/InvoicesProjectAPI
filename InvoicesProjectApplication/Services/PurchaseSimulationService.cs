using System.Globalization;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Interfaces;

namespace InvoicesProjectApplication.Services;

public class PurchaseSimulationService : IPurchaseSimulationService
{
    private readonly IDebtRepository _debtRepository;
    private readonly IReceivableRepository _receivableRepository;
    private readonly ICardPurchaseRepository _cardPurchaseRepository;
    private readonly ICreditCardRepository _creditCardRepository;

    private static readonly string[] MonthNames =
        ["Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez"];

    public PurchaseSimulationService(
        IDebtRepository debtRepository,
        IReceivableRepository receivableRepository,
        ICardPurchaseRepository cardPurchaseRepository,
        ICreditCardRepository creditCardRepository)
    {
        _debtRepository = debtRepository;
        _receivableRepository = receivableRepository;
        _cardPurchaseRepository = cardPurchaseRepository;
        _creditCardRepository = creditCardRepository;
    }

    public async Task<PurchaseSimulationResultDto> SimulateAsync(Guid userId, PurchaseSimulationRequestDto request)
    {
        var items = request.Items.Select(i => new SimulationItemResultDto(
            i.Description,
            i.Quantity,
            i.UnitPrice,
            i.Quantity * i.UnitPrice
        )).ToList();

        var totalAmount = items.Sum(i => i.Subtotal);

        var userCards = (await _creditCardRepository.GetByUserIdAsync(userId)).ToList();

        // Build card info with available limits
        var cardInfos = new List<SimulationCardInfoDto>();
        foreach (var card in userCards.Where(c => c.IsActive))
        {
            var pending = await _cardPurchaseRepository.GetTotalPendingByCardIdAsync(card.Id);
            var available = card.CreditLimit.HasValue ? card.CreditLimit.Value - pending : (decimal?)null;
            cardInfos.Add(new SimulationCardInfoDto(
                card.Id,
                card.Name,
                card.LastFourDigits,
                card.CreditLimit,
                pending,
                available
            ));
        }

        // Pre-fetch financial data for all projection months (avoid repeated queries)
        var now = DateTime.UtcNow;
        var startDate = (request.StartYear.HasValue && request.StartMonth.HasValue)
            ? new DateTime(request.StartYear.Value, request.StartMonth.Value, 1)
            : new DateTime(now.Year, now.Month, 1).AddMonths(1);

        var monthlyData = new List<(int Year, int Month, string Label, decimal Debts, decimal Receivables, decimal CardPurchases)>();
        for (int i = 0; i < request.ProjectionMonths; i++)
        {
            var target = startDate.AddMonths(i);
            var year = target.Year;
            var month = target.Month;
            var label = $"{MonthNames[month - 1]}/{year}";

            var debts = await _debtRepository.GetByUserIdAndMonthAsync(userId, year, month);
            var receivables = await _receivableRepository.GetByUserIdAndMonthAsync(userId, year, month);
            var cardPurchases = await _cardPurchaseRepository.GetByUserIdAndMonthAsync(userId, year, month);

            var existingDebts = debts.Where(d => !d.IsPaid).Sum(d => d.Amount);
            var totalReceivables = receivables.Where(r => !r.IsReceived).Sum(r => r.Amount);
            var existingCardPurchases = cardPurchases.Where(p => !p.IsPaid).Sum(p => p.Amount / p.Installments);

            monthlyData.Add((year, month, label, existingDebts, totalReceivables, existingCardPurchases));
        }

        // Process each plan
        var plans = new List<SimulationPlanResultDto>();
        foreach (var plan in request.Plans)
        {
            var allocations = new List<SimulationAllocationResultDto>();
            var hasLimitIssues = false;

            foreach (var alloc in plan.Allocations)
            {
                var card = userCards.FirstOrDefault(c => c.Id == alloc.CreditCardId);
                var cardInfo = cardInfos.FirstOrDefault(c => c.Id == alloc.CreditCardId);

                var installments = Math.Max(1, alloc.Installments);
                var installmentValue = Math.Round(alloc.Amount / installments, 2);
                var exceedsLimit = cardInfo?.AvailableLimit.HasValue == true && alloc.Amount > cardInfo.AvailableLimit.Value;

                if (exceedsLimit) hasLimitIssues = true;

                allocations.Add(new SimulationAllocationResultDto(
                    alloc.CreditCardId,
                    card?.Name ?? "Desconhecido",
                    card?.LastFourDigits ?? "????",
                    alloc.Amount,
                    installments,
                    installmentValue,
                    cardInfo?.AvailableLimit,
                    exceedsLimit
                ));
            }

            var totalAllocated = allocations.Sum(a => a.Amount);
            var unallocated = totalAmount - totalAllocated;

            // Build monthly projections for this plan
            var projections = new List<SimulationMonthProjectionDto>();
            for (int i = 0; i < monthlyData.Count; i++)
            {
                var md = monthlyData[i];

                // Sum all simulated installments for this month across all allocations
                var simulatedInstallment = allocations
                    .Where(a => i < a.Installments)
                    .Sum(a => a.InstallmentValue);

                var totalExpenses = md.Debts + md.CardPurchases + simulatedInstallment;
                var balance = md.Receivables - totalExpenses;

                projections.Add(new SimulationMonthProjectionDto(
                    md.Year,
                    md.Month,
                    md.Label,
                    md.Receivables,
                    md.Debts,
                    md.CardPurchases,
                    simulatedInstallment,
                    totalExpenses,
                    balance
                ));
            }

            plans.Add(new SimulationPlanResultDto(
                plan.Label,
                allocations,
                totalAllocated,
                unallocated,
                hasLimitIssues,
                projections
            ));
        }

        return new PurchaseSimulationResultDto(items, totalAmount, cardInfos, plans);
    }
}
