using System.Security.Claims;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoicesProjectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DebtsController : ControllerBase
{
    private readonly IDebtService _debtService;

    public DebtsController(IDebtService debtService)
    {
        _debtService = debtService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Lista todos os débitos do usuário
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DebtDto>>> GetAll()
    {
        var debts = await _debtService.GetByUserIdAsync(GetUserId());
        return Ok(debts);
    }

    /// <summary>
    /// Lista débitos pendentes do usuário
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<DebtDto>>> GetPending()
    {
        var debts = await _debtService.GetPendingByUserIdAsync(GetUserId());
        return Ok(debts);
    }

    /// <summary>
    /// Obtém um débito por ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DebtDto>> GetById(Guid id)
    {
        var debt = await _debtService.GetByIdAsync(id);
        if (debt is null)
            return NotFound();

        return Ok(debt);
    }

    /// <summary>
    /// Cria um novo débito
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<DebtDto>> Create([FromBody] CreateDebtDto dto)
    {
        var debt = await _debtService.CreateAsync(GetUserId(), dto);
        return CreatedAtAction(nameof(GetById), new { id = debt.Id }, debt);
    }

    [HttpPost("installment")]
    public async Task<ActionResult<IEnumerable<DebtDto>>> CreateInstallment([FromBody] CreateInstallmentDebtDto dto)
    {
        try
        {
            var debts = await _debtService.CreateInstallmentAsync(GetUserId(), dto);
            return Ok(debts);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("recurring")]
    public async Task<ActionResult<IEnumerable<DebtDto>>> CreateRecurring([FromBody] CreateRecurringDebtDto dto)
    {
        try
        {
            var debts = await _debtService.CreateRecurringAsync(GetUserId(), dto);
            return Ok(debts);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Atualiza um débito
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DebtDto>> Update(Guid id, [FromBody] UpdateDebtDto dto)
    {
        try
        {
            var debt = await _debtService.UpdateAsync(id, dto);
            return Ok(debt);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Marca um débito como pago
    /// </summary>
    [HttpPost("{id:guid}/pay")]
    public async Task<ActionResult<DebtDto>> MarkAsPaid(Guid id)
    {
        try
        {
            var debt = await _debtService.MarkAsPaidAsync(id);
            return Ok(debt);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Remove um débito
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _debtService.DeleteAsync(id);
        return NoContent();
    }

    [HttpDelete("group/{groupId:guid}")]
    public async Task<IActionResult> DeleteGroup(Guid groupId)
    {
        await _debtService.DeleteGroupAsync(groupId);
        return NoContent();
    }
}
