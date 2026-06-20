using Nexora.Domain.Entities;
using System.Threading.Tasks;

namespace Nexora.Domain.Repositories
{
    public interface IInvoiceRepository
    {
        Task<Invoice?> GetByIdAsync(long id);
        Task AddAsync(Invoice invoice);
        Task UpdateAsync(Invoice invoice);
    }
}
