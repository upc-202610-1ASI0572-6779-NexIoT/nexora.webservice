using System.Threading.Tasks;
using Nexora.Domain.Entities;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Repositories
{
    public class MaintenanceTicketRepository : IMaintenanceTicketRepository
    {
        private readonly NexoraDbContext _context;

        public MaintenanceTicketRepository(NexoraDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(MaintenanceTicket ticket)
        {
            await _context.MaintenanceTickets.AddAsync(ticket);
        }
    }
}
