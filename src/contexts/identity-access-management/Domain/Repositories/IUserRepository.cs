using Nexora.Domain.Entities;
using System.Threading.Tasks;

namespace Nexora.Domain.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByIdAsync(long id);
        Task AddAsync(User user);
        Task UpdateAsync(User user);
    }
}
