using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoicesProjectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CardPurchasesController : ControllerBase
{
    private readonly ICardPurchaseService _cardPurchaseService;

    public CardPurchasesController(ICardPurchaseService cardPurchaseService)
    {
        _cardPurchaseService = cardPurchaseService;
    }

    /// <summary>
    /// Lista compras de um cartão
    /// </summary>
    [HttpGet("card/{cardId:guid}")]
    public async Task<ActionResult<IEnumerable<CardPurchaseDto>>> GetByCard(Guid cardId)
    {
        var purchases = await _cardPurchaseService.GetByCreditCardIdAsync(cardId);
        return Ok(purchases);
    }

    /// <summary>
    /// Lista compras pendentes de um cartão
    /// </summary>
    [HttpGet("card/{cardId:guid}/pending")]
    public async Task<ActionResult<IEnumerable<CardPurchaseDto>>> GetPendingByCard(Guid cardId)
    {
        var purchases = await _cardPurchaseService.GetPendingByCreditCardIdAsync(cardId);
        return Ok(purchases);
    }

    /// <summary>
    /// Obtém uma compra por ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CardPurchaseDto>> GetById(Guid id)
    {
        var purchase = await _cardPurchaseService.GetByIdAsync(id);
        if (purchase is null)
            return NotFound();

        return Ok(purchase);
    }

    /// <summary>
    /// Cria uma nova compra
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CardPurchaseDto>> Create([FromBody] CreateCardPurchaseDto dto)
    {
        try
        {
            var purchase = await _cardPurchaseService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = purchase.Id }, purchase);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Atualiza uma compra
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CardPurchaseDto>> Update(Guid id, [FromBody] UpdateCardPurchaseDto dto)
    {
        try
        {
            var purchase = await _cardPurchaseService.UpdateAsync(id, dto);
            return Ok(purchase);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Marca uma compra como paga
    /// </summary>
    [HttpPost("{id:guid}/pay")]
    public async Task<ActionResult<CardPurchaseDto>> MarkAsPaid(Guid id)
    {
        try
        {
            var purchase = await _cardPurchaseService.MarkAsPaidAsync(id);
            return Ok(purchase);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Remove uma compra
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _cardPurchaseService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Simula antecipação de parcelas com desconto
    /// </summary>
    [HttpPost("{id:guid}/simulate-anticipation")]
    public async Task<ActionResult<AnticipationSimulationDto>> SimulateAnticipation(
        Guid id, [FromBody] AnticipationSimulationRequestDto dto)
    {
        try
        {
            var simulation = await _cardPurchaseService.SimulateAnticipationAsync(id, dto.MonthlyDiscountRate);
            return Ok(simulation);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
