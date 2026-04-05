using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;
using InvoicesProjectInfra.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoicesProjectInfra.Repositories
{
    public class TagEventoRepository : ITagEventoRepository
    {
        private readonly AppDbContext _context;
        public TagEventoRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<TagEvento?> GetByIdAsync(Guid id)
        {
            return await _context.TagEventos
                .Include(t => t.Debts)
                .Include(t => t.CardPurchases)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<IEnumerable<TagEvento>> GetByUserIdAsync(Guid userId)
        {
            return await _context.TagEventos
                .Include(t => t.Debts)
                .Include(t => t.CardPurchases)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<TagEvento> AddAsync(TagEvento tagEvento)
        {
            _context.TagEventos.Add(tagEvento);
            
            await _context.SaveChangesAsync();
            return tagEvento;
        }

        public async Task<TagEvento> UpdateAsync(TagEvento tagEvento)
        {
            _context.TagEventos.Update(tagEvento);
            await _context.SaveChangesAsync();
            return tagEvento;
        }

        public async Task DeleteAsync(Guid id)
        {
            var tag = await _context.TagEventos.FindAsync(id);
            if (tag != null)
            {
                _context.TagEventos.Remove(tag);
                await _context.SaveChangesAsync();
            }
        }
    }
}
