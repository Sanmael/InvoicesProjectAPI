using System.Security.Claims;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoicesProjectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReceivablesController : ControllerBase
{
    private readonly IReceivableService _receivableService;

    public ReceivablesController(IReceivableService receivableService)
    {
        _receivableService = receivableService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Lista todos os recebíveis do usuário
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReceivableDto>>> GetAll()
    {
        var receivables = await _receivableService.GetByUserIdAsync(GetUserId());
        return Ok(receivables);
    }

    /// <summary>
    /// Lista recebíveis pendentes do usuário
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<ReceivableDto>>> GetPending()
    {
        var receivables = await _receivableService.GetPendingByUserIdAsync(GetUserId());
        return Ok(receivables);
    }

    /// <summary>
    /// Obtém um recebível por ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReceivableDto>> GetById(Guid id)
    {
        var receivable = await _receivableService.GetByIdAsync(id);
        if (receivable is null)
            return NotFound();

        return Ok(receivable);
    }

    /// <summary>
    /// Cria um novo recebível
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ReceivableDto>> Create([FromBody] CreateReceivableDto dto)
    {
        var receivable = await _receivableService.CreateAsync(GetUserId(), dto);
        return CreatedAtAction(nameof(GetById), new { id = receivable.Id }, receivable);
    }

    [HttpPost("recurring")]
    public async Task<ActionResult<IEnumerable<ReceivableDto>>> CreateRecurring([FromBody] CreateRecurringReceivableDto dto)
    {
        try
        {
            var receivables = await _receivableService.CreateRecurringAsync(GetUserId(), dto);
            return Ok(receivables);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Atualiza um recebível
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReceivableDto>> Update(Guid id, [FromBody] UpdateReceivableDto dto)
    {
        try
        {
            var receivable = await _receivableService.UpdateAsync(id, dto);
            return Ok(receivable);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Marca um recebível como recebido
    /// </summary>
    [HttpPost("{id:guid}/receive")]
    public async Task<ActionResult<ReceivableDto>> MarkAsReceived(Guid id)
    {
        try
        {
            var receivable = await _receivableService.MarkAsReceivedAsync(id);
            return Ok(receivable);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Remove um recebível
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _receivableService.DeleteAsync(id);
        return NoContent();
    }

    [HttpDelete("group/{groupId:guid}")]
    public async Task<IActionResult> DeleteGroup(Guid groupId)
    {
        await _receivableService.DeleteGroupAsync(groupId);
        return NoContent();
    }
}
