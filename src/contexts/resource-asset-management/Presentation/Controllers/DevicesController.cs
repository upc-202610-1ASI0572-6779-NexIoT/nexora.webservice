using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Persistence;
using Nexora.Domain.Enums;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;
using Nexora.Shared.Infrastructure;

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

            var propertyIds = new List<long>();

            var landlord = await _context.Landlords
                .FirstOrDefaultAsync(l => l.UserId == userId);
            
            if (landlord != null)
            {
                propertyIds = await _context.Properties
                    .Where(p => p.LandlordId == landlord.Id)
                    .Select(p => p.Id)
                    .ToListAsync();
            }
            else
            {
                var tenant = await _context.Tenants
                    .FirstOrDefaultAsync(t => t.UserId == userId);
                if (tenant != null && tenant.PropertyId.HasValue)
                {
                    propertyIds.Add(tenant.PropertyId.Value);
                }
                else
                {
                    return Ok(new List<object>()); // Return empty list for unlinked tenants
                }
            }

            var devices = await _context.Devices
                .Where(d => landlord != null 
                    ? (d.PropertyId == null || propertyIds.Contains(d.PropertyId.Value))
                    : (d.PropertyId != null && propertyIds.Contains(d.PropertyId.Value)))
                .Select(d => new {
                    d.Id,
                    d.Name,
                    d.MacAddress,
                    d.Rssi,
                    d.FirmwareVersion,
                    IsFirmwareOutdated = d.FirmwareVersion != null && d.FirmwareVersion != "v2.4.1",
                    ConnectionStatus = d.ConnectionStatus.ToString(),
                    d.LastSyncAt,
                    d.PropertyId,
                    PropertyName = d.Property != null ? d.Property.Name : "Unassigned",
                    LatestReading = _context.TelemetryLogs
                        .Where(t => t.DeviceId == d.Id)
                        .OrderByDescending(t => t.Timestamp)
                        .Select(t => d.Id.Contains("gas") ? Math.Round(t.GasReading, 3).ToString() + " ppm" :
                                     d.Id.Contains("water") ? Math.Round(t.WaterReading, 3).ToString() + " L/min" :
                                     d.Id.Contains("volt") ? (t.VoltageOk ? "220 V (Normal)" : "Low Voltage") :
                                     Math.Round(t.ElectricityReading, 3).ToString() + " A")
                        .FirstOrDefault() ?? (d.ConnectionStatus == ConnectionStatus.Online ? "Active" : "Offline")
                })
                .ToListAsync();

            var devicesWithValveState = devices.Select(d => new {
                d.Id,
                d.Name,
                d.MacAddress,
                d.Rssi,
                d.FirmwareVersion,
                d.IsFirmwareOutdated,
                d.ConnectionStatus,
                d.LastSyncAt,
                d.PropertyId,
                d.PropertyName,
                d.LatestReading,
                ValveState = DeviceCommandQueue.ValveStates.TryGetValue(d.Id, out var state) ? state : "OPEN"
            }).ToList();

            return Ok(devicesWithValveState);
        }

        [HttpGet("kpis")]
        public async Task<IActionResult> GetKPIs()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var propertyIds = new List<long>();

            var landlord = await _context.Landlords
                .FirstOrDefaultAsync(l => l.UserId == userId);
            
            if (landlord != null)
            {
                propertyIds = await _context.Properties
                    .Where(p => p.LandlordId == landlord.Id)
                    .Select(p => p.Id)
                    .ToListAsync();
            }
            else
            {
                var tenant = await _context.Tenants
                    .FirstOrDefaultAsync(t => t.UserId == userId);
                if (tenant != null && tenant.PropertyId.HasValue)
                {
                    propertyIds.Add(tenant.PropertyId.Value);
                }
                else
                {
                    return Ok(new {
                        operationalStatus = "100%",
                        gatewayLoad = "0.00",
                        activeAlerts = "0",
                        firmwareDrift = "0"
                    });
                }
            }

            var devices = await _context.Devices
                .Where(d => d.PropertyId != null && propertyIds.Contains(d.PropertyId.Value))
                .ToListAsync();

            var total = devices.Count;
            var online = devices.Count(d => d.ConnectionStatus == ConnectionStatus.Online);
            var offline = total - online;
            var outdated = devices.Count(d => d.FirmwareVersion != null && d.FirmwareVersion != "v2.4.1");

            var opStatus = total > 0 ? $"{Math.Round(((double)online / total) * 100)}%" : "100%";

            // Calculate real gateway load: average messages per second in the last 60 seconds
            var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            var deviceIds = devices.Select(d => d.Id).ToList();
            var msgCount = await _context.TelemetryLogs
                .CountAsync(t => deviceIds.Contains(t.DeviceId) && t.Timestamp >= oneMinuteAgo);

            var load = Math.Round((double)msgCount / 60.0, 2);

            // Active alerts count for landlord's devices
            var activeAlertsCount = await _context.Alerts
                .CountAsync(a => deviceIds.Contains(a.DeviceId));

            return Ok(new {
                operationalStatus = opStatus,
                gatewayLoad = load.ToString("F2"),
                activeAlerts = activeAlertsCount.ToString(),
                firmwareDrift = outdated.ToString()
            });
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
            
            var existing = await _context.Devices.FirstOrDefaultAsync(d => d.Id == request.Id);
            if (existing != null)
            {
                if (existing.PropertyId != null)
                {
                    return BadRequest("Device with this serial number is already registered to another property.");
                }

                // If the device exists in the pool but is unassigned, allow claiming it by assigning it to the property
                if (request.PropertyId.HasValue)
                {
                    var propertyExists = await _context.Properties.AnyAsync(p => p.Id == request.PropertyId.Value);
                    if (!propertyExists) return BadRequest("Target property not found.");
                    existing.AssignToProperty(request.PropertyId.Value);
                }

                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    existing.UpdateName(request.Name);
                }

                if (!string.IsNullOrWhiteSpace(request.MacAddress))
                {
                    var existingMac = await _context.Devices.AnyAsync(d => d.MacAddress == request.MacAddress && d.Id != existing.Id);
                    if (existingMac) return BadRequest("Device with this MAC Address is already registered.");
                    existing.UpdateMacAddress(request.MacAddress);
                }

                await _context.SaveChangesAsync();
                return Ok(existing);
            }

            if (!string.IsNullOrWhiteSpace(request.MacAddress))
            {
                var existingMac = await _context.Devices.AnyAsync(d => d.MacAddress == request.MacAddress);
                if (existingMac) return BadRequest("Device with this MAC Address is already registered.");
            }
            
            var device = new Nexora.Domain.Entities.Device(request.Id, ConnectionStatus.Online, DateTime.UtcNow, request.MacAddress, request.Name);
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

        [HttpPost("{id}/command")]
        public async Task<IActionResult> SendCommand(string id, [FromBody] DeviceCommandRequest request)
        {
            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == id);
            if (device == null) return NotFound("Device not found.");

            DeviceCommandQueue.PendingCommands[id] = request.Command;

            // Update state immediately to make the mobile app UI highly responsive
            if (request.Command == "CLOSE_VALVE")
            {
                DeviceCommandQueue.ValveStates[id] = "CLOSED";
            }
            else if (request.Command == "OPEN_VALVE")
            {
                DeviceCommandQueue.ValveStates[id] = "OPEN";
            }

            return Ok(new { message = $"Command {request.Command} successfully queued for device {id}." });
        }
    }

    public record AssignDeviceRequest(long? PropertyId);
    public record RegisterDeviceRequest(string Id, string? Name, long? PropertyId, string? MacAddress);
    public record DeviceCommandRequest(string Command);
}
