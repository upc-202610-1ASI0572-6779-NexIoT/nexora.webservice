using Nexora.Domain.Entities;

namespace Nexora.Application.Services
{
    public class CheckSystemHealthUseCase
    {
        public SystemStatus Execute()
        {
            return new SystemStatus("Nexora API is running successfully!", "Development");
        }
    }
}
