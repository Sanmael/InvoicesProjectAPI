using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

public interface ITagEventoService
{
    Task<TagEventoDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<TagEventoDto>> GetByUserIdAsync(Guid userId);
    Task<TagEventoDto> CreateAsync(Guid userId, CreateTagEventoDto dto);
    Task<TagEventoDto> UpdateAsync(Guid id, UpdateTagEventoDto dto);
    Task DeleteAsync(Guid id);
}
