using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

public interface IBankImportService
{
    Task<BankImportResultDto> ParseOfxAsync(Stream fileStream, string fileName);
    Task<BankImportResultDto> ParseCsvAsync(Stream fileStream, string fileName);
    Task<ImportResultDto> ConfirmImportAsync(Guid userId, ConfirmBankImportDto dto);
}
