using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

public interface IFinancialSummaryService
{
    Task<FinancialSummaryDto> GetMonthlySummaryAsync(Guid userId, int year, int month);
    Task<FinancialSummaryDto> GetCurrentMonthSummaryAsync(Guid userId);
    Task<FinancialScoreDto> GetFinancialScoreAsync(Guid userId);
}
