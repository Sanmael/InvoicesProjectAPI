using System.Security.Claims;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoicesProjectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PurchaseSimulationController : ControllerBase
{
    private readonly IPurchaseSimulationService _simulationService;

    public PurchaseSimulationController(IPurchaseSimulationService simulationService)
    {
        _simulationService = simulationService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("simulate")]
    public async Task<ActionResult<PurchaseSimulationResultDto>> Simulate(
        [FromBody] PurchaseSimulationRequestDto request)
    {
        if (!request.Items.Any())
            return BadRequest(new { message = "Adicione pelo menos um item." });

        if (!request.Plans.Any())
            return BadRequest(new { message = "Adicione pelo menos um plano de pagamento." });

        var result = await _simulationService.SimulateAsync(GetUserId(), request);
        return Ok(result);
    }
}
