using System.Threading.Tasks;
using Nexora.Domain.Enums;

namespace Nexora.Domain.Services
{
    public interface IPropertyCodeGenerator
    {
        Task<string> GenerateAsync(PropertyType type);
    }
}
