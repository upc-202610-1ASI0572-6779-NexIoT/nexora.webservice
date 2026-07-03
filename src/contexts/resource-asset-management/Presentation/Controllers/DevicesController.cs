using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Persistence;
using Nexora.Domain.Enums;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;

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

            var landlord = await _context.Landlords
                .FirstOrDefaultAsync(l => l.UserId == userId);
            if (landlord == null) return NotFound("Landlord profile not found.");

            var propertyIds = await _context.Properties
                .Where(p => p.LandlordId == landlord.Id)
                .Select(p => p.Id)
                .ToListAsync();

            var devices = await _context.Devices
                .Where(d => d.PropertyId == null || propertyIds.Contains(d.PropertyId.Value))
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

        [HttpPut("{id}/assign")]
        public async Task<IActionResult> AssignDevice(string id, [FromBody] AssignDeviceRequest request)
        {
            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == id);
            if (device == null) return NotFound("Device not found.");

            if (request.PropertyId.HasValue)
            {
                var propertyExists = await _context.Properties.AnyAsync(p => p.Id == request.PropertyId.Value);
                if (!propertyExists) return BadRequest("Target property not found.");
                device.AssignToProperty(request.PropertyId.Value);
            }
            else
            {
                device.AssignToProperty(null);
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost]
        public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Id)) return BadRequest("Device ID is required.");
            
            var existing = await _context.Devices.AnyAsync(d => d.Id == request.Id);
            if (existing) return BadRequest("Device with this serial number is already registered.");
            
            var device = new Nexora.Domain.Entities.Device(request.Id, ConnectionStatus.Online, DateTime.UtcNow);
            if (request.PropertyId.HasValue)
            {
                var propertyExists = await _context.Properties.AnyAsync(p => p.Id == request.PropertyId.Value);
                if (!propertyExists) return BadRequest("Target property not found.");
                device.AssignToProperty(request.PropertyId.Value);
            }
            
            _context.Devices.Add(device);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAll), new { id = device.Id }, device);
        }

        [HttpPut("{id}/reboot")]
        public async Task<IActionResult> RebootDevice(string id)
        {
            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == id);
            if (device == null) return NotFound("Device not found.");

            device.UpdateSync(ConnectionStatus.Offline, DateTime.UtcNow);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    public record AssignDeviceRequest(long? PropertyId);
    public record RegisterDeviceRequest(string Id, long? PropertyId);
}
