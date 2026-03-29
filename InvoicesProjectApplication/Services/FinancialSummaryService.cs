using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Interfaces;

namespace InvoicesProjectApplication.Services;

public class FinancialSummaryService : IFinancialSummaryService
{
    private readonly IDebtRepository _debtRepository;
    private readonly IReceivableRepository _receivableRepository;
    private readonly ICardPurchaseRepository _cardPurchaseRepository;
    private readonly ICreditCardRepository _creditCardRepository;
    private readonly ISavingsGoalRepository _savingsGoalRepository;

    public FinancialSummaryService(
        IDebtRepository debtRepository,
        IReceivableRepository receivableRepository,
        ICardPurchaseRepository cardPurchaseRepository,
        ICreditCardRepository creditCardRepository,
        ISavingsGoalRepository savingsGoalRepository)
    {
        _debtRepository = debtRepository;
        _receivableRepository = receivableRepository;
        _cardPurchaseRepository = cardPurchaseRepository;
        _creditCardRepository = creditCardRepository;
        _savingsGoalRepository = savingsGoalRepository;
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

    public async Task<FinancialScoreDto> GetFinancialScoreAsync(Guid userId)
    {
        var allDebts = await _debtRepository.GetByUserIdAsync(userId);
        var allReceivables = await _receivableRepository.GetByUserIdAsync(userId);
        var cards = await _creditCardRepository.GetByUserIdAsync(userId);
        var goals = await _savingsGoalRepository.GetByUserIdAsync(userId);
        var tips = new List<string>();

        // 1. Payment Discipline (0-35)
        var debtsList = allDebts.ToList();
        int paymentDiscipline = 35;
        if (debtsList.Count > 0)
        {
            var paidDebts = debtsList.Where(d => d.IsPaid).ToList();
            if (paidDebts.Count > 0)
            {
                var paidOnTime = paidDebts.Count(d =>
                    d.PaidAt.HasValue &&
                    DateOnly.FromDateTime(d.PaidAt.Value) <= d.DueDate);
                paymentDiscipline = (int)Math.Round(35m * paidOnTime / paidDebts.Count);
            }
            else
            {
                // No paid debts yet but there are debts
                var overdue = debtsList.Count(d => !d.IsPaid && d.DueDate < DateOnly.FromDateTime(DateTime.UtcNow));
                paymentDiscipline = overdue > 0 ? Math.Max(0, 35 - overdue * 5) : 25;
            }

            if (paymentDiscipline < 25)
                tips.Add("Priorize pagar suas contas em dia para melhorar sua pontuação.");
        }

        // 2. Credit Utilization (0-25)
        int creditUtilization = 25;
        var activeCards = cards.Where(c => c.IsActive && c.CreditLimit.HasValue).ToList();
        if (activeCards.Count > 0)
        {
            decimal totalLimit = 0;
            decimal totalUsed = 0;
            foreach (var card in activeCards)
            {
                totalLimit += card.CreditLimit!.Value;
                var pending = await _cardPurchaseRepository.GetTotalPendingByCardIdAsync(card.Id);
                totalUsed += pending;
            }

            if (totalLimit > 0)
            {
                var utilizationRatio = totalUsed / totalLimit;
                creditUtilization = utilizationRatio switch
                {
                    <= 0.10m => 25,
                    <= 0.30m => 22,
                    <= 0.50m => 18,
                    <= 0.70m => 12,
                    <= 0.90m => 6,
                    _ => 0
                };

                if (utilizationRatio > 0.50m)
                    tips.Add($"Sua utilização de crédito está em {utilizationRatio:P0}. Tente manter abaixo de 30%.");
            }
        }

        // 3. Savings Rate (0-20)
        int savingsRate = 10;
        var now = DateTime.UtcNow;
        var last3MonthsDebts = debtsList.Where(d =>
        {
            var diff = (now.Year * 12 + now.Month) - (d.DueDate.Year * 12 + d.DueDate.Month);
            return diff >= 0 && diff < 3;
        }).Sum(d => d.Amount);

        var last3MonthsReceivables = allReceivables.Where(r =>
        {
            var d = r.ExpectedDate;
            var diff = (now.Year * 12 + now.Month) - (d.Year * 12 + d.Month);
            return diff >= 0 && diff < 3;
        }).Sum(r => r.Amount);

        if (last3MonthsReceivables > 0)
        {
            var surplus = last3MonthsReceivables - last3MonthsDebts;
            var ratio = surplus / last3MonthsReceivables;
            savingsRate = ratio switch
            {
                >= 0.3m => 20,
                >= 0.2m => 17,
                >= 0.1m => 14,
                >= 0m => 10,
                >= -0.1m => 6,
                _ => 0
            };

            if (ratio < 0.1m)
                tips.Add("Tente economizar pelo menos 10% da sua renda mensal.");
        }
        else
        {
            savingsRate = 5;
            tips.Add("Cadastre suas receitas para uma análise mais precisa.");
        }

        // 4. Goal Progress (0-10)
        int goalProgress = 5;
        var goalsList = goals.ToList();
        if (goalsList.Count > 0)
        {
            var completedGoals = goalsList.Count(g => g.IsCompleted);
            var activeGoals = goalsList.Where(g => !g.IsCompleted).ToList();
            var onTrack = 0;

            foreach (var goal in activeGoals)
            {
                if (!goal.Deadline.HasValue)
                {
                    onTrack += goal.CurrentAmount > 0 ? 1 : 0;
                    continue;
                }

                var totalDays = goal.Deadline.Value.DayNumber - DateOnly.FromDateTime(goal.CreatedAt).DayNumber;
                var elapsed = DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - DateOnly.FromDateTime(goal.CreatedAt).DayNumber;
                if (totalDays <= 0) continue;

                var expectedProgress = (decimal)elapsed / totalDays;
                var actualProgress = goal.TargetAmount > 0 ? goal.CurrentAmount / goal.TargetAmount : 0;
                if (actualProgress >= expectedProgress * 0.8m)
                    onTrack++;
            }

            var total = completedGoals + activeGoals.Count;
            goalProgress = total > 0 ? (int)Math.Round(10m * (completedGoals + onTrack) / total) : 5;

            if (goalProgress < 5 && activeGoals.Count > 0)
                tips.Add("Suas metas estão atrasadas. Considere aportar mais ou ajustar os prazos.");
        }
        else
        {
            goalProgress = 0;
            tips.Add("Crie metas financeiras para acompanhar seu progresso de economia.");
        }

        // 5. Financial Organization (0-10)
        int financialOrganization = 0;
        if (allReceivables.Any()) financialOrganization += 2;
        if (debtsList.Count > 0) financialOrganization += 1;
        if (activeCards.Count > 0) financialOrganization += 2;
        if (goalsList.Count > 0) financialOrganization += 2;
        var categories = debtsList.Select(d => d.Category).Distinct().Count();
        if (categories >= 3) financialOrganization += 2;
        else if (categories >= 1) financialOrganization += 1;
        financialOrganization = Math.Min(10, financialOrganization);

        if (financialOrganization < 5)
            tips.Add("Categorize suas despesas e organize suas finanças para ter mais controle.");

        // Calculate total and classification
        int totalScore = paymentDiscipline + creditUtilization + savingsRate + goalProgress + financialOrganization;
        totalScore = Math.Min(100, Math.Max(0, totalScore));

        var classification = totalScore switch
        {
            >= 90 => "Excelente",
            >= 70 => "Bom",
            >= 50 => "Regular",
            >= 30 => "Atenção",
            _ => "Crítico"
        };

        if (totalScore >= 80 && tips.Count == 0)
            tips.Add("Parabéns! Sua saúde financeira está ótima. Continue assim!");

        return new FinancialScoreDto(
            totalScore,
            classification,
            new FinancialScoreBreakdownDto(
                paymentDiscipline, 35,
                creditUtilization, 25,
                savingsRate, 20,
                goalProgress, 10,
                financialOrganization, 10),
            tips);
    }
}
