using System.Security.Claims;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoicesProjectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SavingsGoalsController : ControllerBase
{
    private readonly ISavingsGoalService _savingsGoalService;

    public SavingsGoalsController(ISavingsGoalService savingsGoalService)
    {
        _savingsGoalService = savingsGoalService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SavingsGoalDto>>> GetAll()
    {
        var goals = await _savingsGoalService.GetByUserIdAsync(GetUserId());
        return Ok(goals);
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<SavingsGoalDto>>> GetActive()
    {
        var goals = await _savingsGoalService.GetActiveByUserIdAsync(GetUserId());
        return Ok(goals);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SavingsGoalDto>> GetById(Guid id)
    {
        var goal = await _savingsGoalService.GetByIdAsync(id);
        if (goal is null)
            return NotFound();

        return Ok(goal);
    }

    [HttpPost]
    public async Task<ActionResult<SavingsGoalDto>> Create([FromBody] CreateSavingsGoalDto dto)
    {
        var goal = await _savingsGoalService.CreateAsync(GetUserId(), dto);
        return CreatedAtAction(nameof(GetById), new { id = goal.Id }, goal);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SavingsGoalDto>> Update(Guid id, [FromBody] UpdateSavingsGoalDto dto)
    {
        try
        {
            var goal = await _savingsGoalService.UpdateAsync(id, dto);
            return Ok(goal);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/add")]
    public async Task<ActionResult<SavingsGoalDto>> AddAmount(Guid id, [FromBody] AddSavingsAmountDto dto)
    {
        try
        {
            var goal = await _savingsGoalService.AddAmountAsync(id, dto.Amount);
            return Ok(goal);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _savingsGoalService.DeleteAsync(id);
        return NoContent();
    }
}
