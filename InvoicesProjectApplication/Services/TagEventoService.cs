using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;

namespace InvoicesProjectApplication.Services;

public class TagEventoService : ITagEventoService
{
    private readonly ITagEventoRepository _repository;

    public TagEventoService(ITagEventoRepository repository)
    {
        _repository = repository;
    }

    public async Task<TagEventoDto?> GetByIdAsync(Guid id)
    {
        var tag = await _repository.GetByIdAsync(id);
        return tag is null ? null : MapToDto(tag);
    }

    public async Task<IEnumerable<TagEventoDto>> GetByUserIdAsync(Guid userId)
    {
        var tags = await _repository.GetByUserIdAsync(userId);
        return tags.Select(MapToDto);
    }

    public async Task<TagEventoDto> CreateAsync(Guid userId, CreateTagEventoDto dto)
    {
        var tag = new TagEvento
        {
            UserId = userId,
            Nome = dto.Nome,
            Descricao = dto.Descricao,
            DataInicio = NormalizeToUtc(dto.DataInicio),
            DataFim = NormalizeToUtc(dto.DataFim)
        };

        await _repository.AddAsync(tag);
        return MapToDto(tag);
    }

    public async Task<TagEventoDto> UpdateAsync(Guid id, UpdateTagEventoDto dto)
    {
        var tag = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Evento não encontrado.");

        if (dto.Nome is not null)
            tag.Nome = dto.Nome;

        if (dto.Descricao is not null)
            tag.Descricao = dto.Descricao;

        if (dto.DataInicio.HasValue)
            tag.DataInicio = NormalizeToUtc(dto.DataInicio);

        if (dto.DataFim.HasValue)
            tag.DataFim = NormalizeToUtc(dto.DataFim);

        tag.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(tag);
        return MapToDto(tag);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static TagEventoDto MapToDto(TagEvento tag) =>
        new(
            tag.Id,
            tag.Nome,
            tag.Descricao,
            tag.DataInicio,
            tag.DataFim,
            tag.CreatedAt,
            tag.Debts?.Count ?? 0,
            tag.CardPurchases?.Count ?? 0,
            (tag.Debts?.Sum(d => d.Amount) ?? 0) + (tag.CardPurchases?.Sum(c => c.Amount) ?? 0)
        );

    private static DateTime? NormalizeToUtc(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }
}
