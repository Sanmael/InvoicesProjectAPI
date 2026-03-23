using System.Security.Claims;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoicesProjectAPI.Controllers;

/// <summary>
/// Controller para histórico e gerenciamento de notificações
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Obtém o histórico de notificações do usuário
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<EmailNotificationDto>>> GetHistory([FromQuery] int limit = 50)
    {
        var userId = GetUserId();
        var history = await _notificationService.GetNotificationHistoryAsync(userId, limit);
        return Ok(history);
    }

    /// <summary>
    /// Obtém o resumo das notificações do usuário
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<NotificationSummaryDto>> GetSummary()
    {
        var userId = GetUserId();
        var summary = await _notificationService.GetNotificationSummaryAsync(userId);
        return Ok(summary);
    }

    /// <summary>
    /// Processa as notificações para o usuário autenticado
    /// </summary>
    [HttpPost("process")]
    public async Task<ActionResult> ProcessNotifications()
    {
        var userId = GetUserId();
        
        try
        {
            await _notificationService.ProcessNotificationsForUserAsync(userId);
            return Ok(new { message = "Notificações processadas." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
