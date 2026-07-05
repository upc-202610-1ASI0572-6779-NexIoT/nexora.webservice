using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Persistence;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/devices")]
    [Authorize]
    public class DevicesController : ControllerBase
    {
        private readonly NexoraDbContext _context;

        public DevicesController(NexoraDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var devices = await _context.Devices
                .Where(d => d.Property != null && d.Property.Landlord.UserId == userId)
                .Select(d => new {
                    d.Id,
                    ConnectionStatus = d.ConnectionStatus.ToString(),
                    d.LastSyncAt,
                    d.PropertyId,
                    PropertyName = d.Property != null ? d.Property.Name : "Unassigned"
                })
                .ToListAsync();

            return Ok(devices);
        }
    }
}
