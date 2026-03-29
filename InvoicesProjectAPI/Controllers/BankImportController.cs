using System.Security.Claims;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoicesProjectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BankImportController : ControllerBase
{
    private readonly IBankImportService _importService;

    public BankImportController(IBankImportService importService)
    {
        _importService = importService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Upload de arquivo OFX (extrato bancário) para importação.
    /// </summary>
    [HttpPost("ofx")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<BankImportResultDto>> ImportOfx(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Nenhum arquivo enviado.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not ".ofx" and not ".qfx")
            return BadRequest("Tipo de arquivo não suportado. Use arquivos .ofx ou .qfx");

        using var stream = file.OpenReadStream();
        var result = await _importService.ParseOfxAsync(stream, file.FileName);
        return Ok(result);
    }

    /// <summary>
    /// Upload de arquivo CSV (extrato bancário) para importação.
    /// </summary>
    [HttpPost("csv")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<BankImportResultDto>> ImportCsv(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Nenhum arquivo enviado.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".csv")
            return BadRequest("Tipo de arquivo não suportado. Use arquivo .csv");

        using var stream = file.OpenReadStream();
        var result = await _importService.ParseCsvAsync(stream, file.FileName);
        return Ok(result);
    }

    /// <summary>
    /// Confirma a importação das transações extraídas.
    /// </summary>
    [HttpPost("confirm")]
    public async Task<ActionResult<ImportResultDto>> Confirm([FromBody] ConfirmBankImportDto dto)
    {
        if (dto.Transactions is null || dto.Transactions.Count == 0)
            return BadRequest("Nenhuma transação para importar.");

        var result = await _importService.ConfirmImportAsync(GetUserId(), dto);
        return Ok(result);
    }
}
