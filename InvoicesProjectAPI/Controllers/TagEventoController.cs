using System.Security.Claims;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoicesProjectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TagEventoController : ControllerBase
{
    private readonly ITagEventoService _tagEventoService;

    public TagEventoController(ITagEventoService tagEventoService)
    {
        _tagEventoService = tagEventoService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Lista todos os eventos/tags do usuário
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TagEventoDto>>> GetAll()
    {
        var tags = await _tagEventoService.GetByUserIdAsync(GetUserId());
        return Ok(tags);
    }

    /// <summary>
    /// Obtém um evento por ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TagEventoDto>> GetById(Guid id)
    {
        var tag = await _tagEventoService.GetByIdAsync(id);
        if (tag is null)
            return NotFound();

        return Ok(tag);
    }

    /// <summary>
    /// Cria um novo evento/tag temporal
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TagEventoDto>> Create([FromBody] CreateTagEventoDto dto)
    {
        var tag = await _tagEventoService.CreateAsync(GetUserId(), dto);
        return CreatedAtAction(nameof(GetById), new { id = tag.Id }, tag);
    }

    /// <summary>
    /// Atualiza um evento/tag
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TagEventoDto>> Update(Guid id, [FromBody] UpdateTagEventoDto dto)
    {
        try
        {
            var tag = await _tagEventoService.UpdateAsync(id, dto);
            return Ok(tag);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Remove um evento/tag
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _tagEventoService.DeleteAsync(id);
        return NoContent();
    }
}
