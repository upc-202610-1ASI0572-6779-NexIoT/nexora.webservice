using System.Threading.Tasks;
using Nexora.Domain.Entities;

namespace Nexora.Domain.Repositories
{
    public interface IAlertRepository
    {
        Task AddAsync(Alert alert);
    }
}
