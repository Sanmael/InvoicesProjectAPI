using System.Security.Claims;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoicesProjectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CreditCardsController : ControllerBase
{
    private readonly ICreditCardService _creditCardService;

    public CreditCardsController(ICreditCardService creditCardService)
    {
        _creditCardService = creditCardService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Lista todos os cartões do usuário
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CreditCardDto>>> GetAll()
    {
        var cards = await _creditCardService.GetByUserIdAsync(GetUserId());
        return Ok(cards);
    }

    /// <summary>
    /// Obtém um cartão por ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CreditCardDto>> GetById(Guid id)
    {
        var card = await _creditCardService.GetByIdAsync(id);
        if (card is null)
            return NotFound();

        return Ok(card);
    }

    /// <summary>
    /// Obtém um cartão com suas compras
    /// </summary>
    [HttpGet("{id:guid}/purchases")]
    public async Task<ActionResult<CreditCardWithPurchasesDto>> GetWithPurchases(Guid id)
    {
        var card = await _creditCardService.GetWithPurchasesAsync(id);
        if (card is null)
            return NotFound();

        return Ok(card);
    }

    /// <summary>
    /// Cria um novo cartão
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CreditCardDto>> Create([FromBody] CreateCreditCardDto dto)
    {
        var card = await _creditCardService.CreateAsync(GetUserId(), dto);
        return CreatedAtAction(nameof(GetById), new { id = card.Id }, card);
    }

    /// <summary>
    /// Atualiza um cartão
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CreditCardDto>> Update(Guid id, [FromBody] UpdateCreditCardDto dto)
    {
        try
        {
            var card = await _creditCardService.UpdateAsync(id, dto);
            return Ok(card);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Remove um cartão
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _creditCardService.DeleteAsync(id);
        return NoContent();
    }
}
