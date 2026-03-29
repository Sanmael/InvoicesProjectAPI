using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

public interface IDocumentImportService
{
    Task<DocumentExtractionResultDto> ExtractFromFileAsync(Stream fileStream, string fileName, string contentType);
    Task<ImportResultDto> ConfirmImportAsync(Guid userId, ConfirmImportDto dto);
}
