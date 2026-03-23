using System.Security.Claims;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoicesProjectAPI.Controllers;

/// <summary>
/// Controller para administração do sistema (somente admins)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IEmailSettingsService _emailSettingsService;
    private readonly INotificationService _notificationService;
    private readonly IUserService _userService;

    public AdminController(
        IEmailSettingsService emailSettingsService,
        INotificationService notificationService,
        IUserService userService)
    {
        _emailSettingsService = emailSettingsService;
        _notificationService = notificationService;
        _userService = userService;
    }

    private async Task<bool> IsAdminAsync()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _userService.GetByIdAsync(userId);
        return user?.IsAdmin ?? false;
    }

    /// <summary>
    /// Obtém as configurações de email do sistema
    /// </summary>
    [HttpGet("email-settings")]
    public async Task<ActionResult<EmailSettingsDto>> GetEmailSettings()
    {
        if (!await IsAdminAsync())
            return Forbid();

        var settings = await _emailSettingsService.GetSettingsAsync();
        
        if (settings is null)
            return NotFound(new { message = "Configurações de email não encontradas." });

        return Ok(settings);
    }

    /// <summary>
    /// Salva as configurações de email do sistema
    /// </summary>
    [HttpPost("email-settings")]
    public async Task<ActionResult<EmailSettingsDto>> SaveEmailSettings([FromBody] EmailSettingsCreateDto dto)
    {
        if (!await IsAdminAsync())
            return Forbid();

        try
        {
            var settings = await _emailSettingsService.SaveSettingsAsync(dto);
            return Ok(settings);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Atualiza apenas a senha do email
    /// </summary>
    [HttpPut("email-settings/password")]
    public async Task<ActionResult> UpdateEmailPassword([FromBody] EmailSettingsPasswordUpdateDto dto)
    {
        if (!await IsAdminAsync())
            return Forbid();

        try
        {
            await _emailSettingsService.UpdatePasswordAsync(dto.Password);
            return Ok(new { message = "Senha atualizada com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Testa as configurações de email enviando um email de teste
    /// </summary>
    [HttpPost("email-settings/test")]
    public async Task<ActionResult<EmailTestResultDto>> TestEmailSettings([FromBody] EmailTestDto dto)
    {
        if (!await IsAdminAsync())
            return Forbid();

        var result = await _emailSettingsService.TestConnectionAsync(dto);
        
        if (result.Success)
            return Ok(result);
        
        return BadRequest(result);
    }

    /// <summary>
    /// Dispara o processamento de notificações para todos os usuários
    /// </summary>
    [HttpPost("notifications/process-all")]
    public async Task<ActionResult> ProcessAllNotifications()
    {
        if (!await IsAdminAsync())
            return Forbid();

        try
        {
            await _notificationService.ProcessAllNotificationsAsync();
            return Ok(new { message = "Notificações processadas com sucesso." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
