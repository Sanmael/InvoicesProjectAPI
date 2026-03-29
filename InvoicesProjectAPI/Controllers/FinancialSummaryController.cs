using System.Security.Claims;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoicesProjectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FinancialSummaryController : ControllerBase
{
    private readonly IFinancialSummaryService _financialSummaryService;

    public FinancialSummaryController(IFinancialSummaryService financialSummaryService)
    {
        _financialSummaryService = financialSummaryService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Obtém o resumo financeiro do mês atual
    /// </summary>
    [HttpGet("current")]
    public async Task<ActionResult<FinancialSummaryDto>> GetCurrentMonth()
    {
        var summary = await _financialSummaryService.GetCurrentMonthSummaryAsync(GetUserId());
        return Ok(summary);
    }

    /// <summary>
    /// Obtém o resumo financeiro de um mês específico
    /// </summary>
    [HttpGet("{year:int}/{month:int}")]
    public async Task<ActionResult<FinancialSummaryDto>> GetByMonth(int year, int month)
    {
        if (month < 1 || month > 12)
            return BadRequest(new { message = "Mês inválido" });

        var summary = await _financialSummaryService.GetMonthlySummaryAsync(GetUserId(), year, month);
        return Ok(summary);
    }

    /// <summary>
    /// Obtém o score de saúde financeira do usuário
    /// </summary>
    [HttpGet("score")]
    public async Task<ActionResult<FinancialScoreDto>> GetFinancialScore()
    {
        var score = await _financialSummaryService.GetFinancialScoreAsync(GetUserId());
        return Ok(score);
    }
}
