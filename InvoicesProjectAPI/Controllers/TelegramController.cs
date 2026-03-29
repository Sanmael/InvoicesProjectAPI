using System.Security.Claims;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoicesProjectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelegramController : ControllerBase
{
    private readonly ITelegramService _telegramService;

    public TelegramController(ITelegramService telegramService)
    {
        _telegramService = telegramService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Gera link para vincular Telegram.
    /// </summary>
    [HttpPost("link")]
    [Authorize]
    public async Task<ActionResult<TelegramLinkDto>> GenerateLink()
    {
        var result = await _telegramService.GenerateLinkAsync(GetUserId());
        return Ok(result);
    }

    /// <summary>
    /// Status da vinculação do Telegram.
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public async Task<ActionResult<TelegramStatusDto>> GetStatus()
    {
        var result = await _telegramService.GetStatusAsync(GetUserId());
        return Ok(result);
    }

    /// <summary>
    /// Ativa/desativa notificações via Telegram.
    /// </summary>
    [HttpPost("toggle")]
    [Authorize]
    public async Task<IActionResult> Toggle([FromBody] TelegramToggleDto dto)
    {
        await _telegramService.ToggleNotificationsAsync(GetUserId(), dto.Enabled);
        return NoContent();
    }

    /// <summary>
    /// Desvincula o Telegram.
    /// </summary>
    [HttpDelete("unlink")]
    [Authorize]
    public async Task<IActionResult> Unlink()
    {
        await _telegramService.UnlinkAsync(GetUserId());
        return NoContent();
    }

    /// <summary>
    /// Webhook do Telegram Bot (chamado pelo Telegram).
    /// Não requer autenticação JWT.
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] TelegramWebhookUpdateDto update)
    {
        await _telegramService.HandleWebhookAsync(update);
        return Ok();
    }
}
