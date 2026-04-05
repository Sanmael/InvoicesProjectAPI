using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InvoicesProjectEntities.Entities;

namespace InvoicesProjectEntities.Interfaces
{
    public interface ITagEventoRepository
    {
        Task<TagEvento?> GetByIdAsync(Guid id);
        Task<IEnumerable<TagEvento>> GetByUserIdAsync(Guid userId);
        Task<TagEvento> AddAsync(TagEvento tagEvento);
        Task<TagEvento> UpdateAsync(TagEvento tagEvento);
        Task DeleteAsync(Guid id);
    }
}
