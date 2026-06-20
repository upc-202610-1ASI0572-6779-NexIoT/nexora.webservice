using System.Threading.Tasks;
using Nexora.Domain.Entities;

namespace Nexora.Domain.Repositories
{
    public interface IMaintenanceTicketRepository
    {
        Task AddAsync(MaintenanceTicket ticket);
    }
}
