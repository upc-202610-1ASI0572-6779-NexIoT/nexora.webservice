using Microsoft.EntityFrameworkCore;
using Nexora.Domain.Entities;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Repositories
{
    public class InvoiceRepository : IInvoiceRepository
    {
        private readonly NexoraDbContext _context;

        public InvoiceRepository(NexoraDbContext context)
        {
            _context = context;
        }

        public async Task<Invoice?> GetByIdAsync(long id)
        {
            return await _context.Invoices
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task AddAsync(Invoice invoice)
        {
            await _context.Invoices.AddAsync(invoice);
        }

        public async Task UpdateAsync(Invoice invoice)
        {
            _context.Invoices.Update(invoice);
            await Task.CompletedTask;
        }
    }
}
