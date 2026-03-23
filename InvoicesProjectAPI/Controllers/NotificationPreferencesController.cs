using System.Security.Claims;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoicesProjectAPI.Controllers;

/// <summary>
/// Controller para preferências de notificação do usuário
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationPreferencesController : ControllerBase
{
    private readonly INotificationPreferenceService _preferenceService;

    public NotificationPreferencesController(INotificationPreferenceService preferenceService)
    {
        _preferenceService = preferenceService;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Obtém as preferências de notificação do usuário autenticado
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<NotificationPreferenceDto>> Get()
    {
        var userId = GetUserId();
        var preferences = await _preferenceService.GetByUserIdAsync(userId);
        
        // Se não existir, retorna os valores padrão
        if (preferences is null)
            return Ok(_preferenceService.GetDefaults());

        return Ok(preferences);
    }

    /// <summary>
    /// Salva as preferências de notificação do usuário autenticado
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<NotificationPreferenceDto>> Save([FromBody] NotificationPreferenceCreateDto dto)
    {
        var userId = GetUserId();
        
        try
        {
            var preferences = await _preferenceService.SaveAsync(userId, dto);
            return Ok(preferences);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Obtém os valores padrão das preferências
    /// </summary>
    [HttpGet("defaults")]
    public ActionResult<NotificationPreferenceDto> GetDefaults()
    {
        return Ok(_preferenceService.GetDefaults());
    }
}
