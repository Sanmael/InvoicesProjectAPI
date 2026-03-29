using System.Security.Claims;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoicesProjectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentImportController : ControllerBase
{
    private readonly IDocumentImportService _importService;

    public DocumentImportController(IDocumentImportService importService)
    {
        _importService = importService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Upload de imagem ou PDF de fatura/nota fiscal para extração via IA.
    /// </summary>
    [HttpPost("extract")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    public async Task<ActionResult<DocumentExtractionResultDto>> Extract(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Nenhum arquivo enviado.");

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "application/pdf" };
        var contentType = file.ContentType.ToLowerInvariant();
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedTypes.Contains(contentType) && ext is not ".jpg" and not ".jpeg" and not ".png" and not ".webp" and not ".pdf")
            return BadRequest("Tipo de arquivo não suportado. Use JPG, PNG, WebP ou PDF.");

        using var stream = file.OpenReadStream();
        var result = await _importService.ExtractFromFileAsync(stream, file.FileName, contentType);
        return Ok(result);
    }

    /// <summary>
    /// Confirma a importação dos itens extraídos, criando-os no sistema.
    /// </summary>
    [HttpPost("confirm")]
    public async Task<ActionResult<ImportResultDto>> Confirm([FromBody] ConfirmImportDto dto)
    {
        if (dto.Items is null || dto.Items.Count == 0)
            return BadRequest("Nenhum item para importar.");

        var result = await _importService.ConfirmImportAsync(GetUserId(), dto);
        return Ok(result);
    }
}
