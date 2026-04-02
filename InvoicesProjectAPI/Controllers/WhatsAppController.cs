using System.Security.Claims;
using System.Text.Json;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoicesProjectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WhatsAppController : ControllerBase
{
    private readonly IWhatsAppService _whatsAppService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WhatsAppController> _logger;

    public WhatsAppController(
        IWhatsAppService whatsAppService,
        IServiceScopeFactory scopeFactory,
        ILogger<WhatsAppController> logger)
    {
        _whatsAppService = whatsAppService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Webhook recebido do serviço WhatsApp.
    /// Retorna 200 imediatamente e processa a mensagem em background.
    /// </summary>
    [HttpPost("webhook")]
    public IActionResult Webhook([FromBody] JsonElement payload)
    {
        // Fire-and-forget com scope próprio para não depender do request scope
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();
                await service.HandleIncomingAsync(payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar mensagem WhatsApp em background");
            }
        });

        return Ok();
    }

    /// <summary>
    /// Vincula um número de WhatsApp à conta do usuário autenticado.
    /// </summary>
    [HttpPost("link")]
    [Authorize]
    public async Task<IActionResult> Link([FromBody] WhatsAppLinkDto dto)
    {
        try
        {
            await _whatsAppService.LinkPhoneNumberAsync(GetUserId(), dto.PhoneNumber);
            return Ok(new { message = "WhatsApp vinculado com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Desvincula o WhatsApp do usuário autenticado.
    /// </summary>
    [HttpDelete("unlink")]
    [Authorize]
    public async Task<IActionResult> Unlink()
    {
        await _whatsAppService.UnlinkAsync(GetUserId());
        return NoContent();
    }

    /// <summary>
    /// Retorna o status da vinculação do WhatsApp.
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public async Task<ActionResult<WhatsAppStatusDto>> GetStatus()
    {
        var result = await _whatsAppService.GetStatusAsync(GetUserId());
        return Ok(result);
    }
}
