using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Nexora.Application.Dto;
using Nexora.Domain.Enums;
using Nexora.Infrastructure.Persistence;
using Nexora.Shared.Domain.Api;
using Nexora.Shared.Infrastructure;
using Nexora.Shared.Domain.Resources;
using Swashbuckle.AspNetCore.Annotations;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/devices")]
    [Authorize]
    [SwaggerTag("Device Management")]
    public class DevicesController : ControllerBase
    {
        private readonly NexoraDbContext _context;
        private readonly IStringLocalizer<SharedMessages> _localizer;

        public DevicesController(NexoraDbContext context, IStringLocalizer<SharedMessages> localizer)
        {
            _context = context;
            _localizer = localizer;
        }

        /// <summary>
        /// Returns all devices accessible to the current user.
        /// </summary>
        [HttpGet]
        [SwaggerOperation(Summary = "List all devices", Description = "Returns all devices the user has access to, with latest readings and valve states.")]
        [ProducesResponseType(typeof(List<DeviceListItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll([FromQuery] long? propertyId = null)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var propertyIds = new List<long>();

            var landlord = await _context.Landlords
                .FirstOrDefaultAsync(l => l.UserId == userId.Value);
            
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
                    .FirstOrDefaultAsync(t => t.UserId == userId.Value);
                if (tenant != null && tenant.PropertyId.HasValue)
                {
                    propertyIds.Add(tenant.PropertyId.Value);
                }
                else
                {
                    return Ok(new List<DeviceListItemDto>());
                }
            }

            var devices = await _context.Devices
                .Where(d => landlord != null 
                    ? (d.PropertyId == null || propertyIds.Contains(d.PropertyId.Value))
                    : (d.PropertyId != null && propertyIds.Contains(d.PropertyId.Value)))
                .Where(d => !propertyId.HasValue || d.PropertyId == propertyId.Value)
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

            var result = devices.Select(d => new DeviceListItemDto(
                d.Id,
                d.Name ?? "Unknown",
                d.MacAddress,
                d.Rssi,
                d.FirmwareVersion,
                d.IsFirmwareOutdated,
                d.ConnectionStatus,
                d.LastSyncAt,
                d.PropertyId,
                d.PropertyName,
                d.LatestReading,
                DeviceCommandQueue.ValveStates.TryGetValue(d.Id, out var state) ? state : "OPEN"
            )).ToList();

            return Ok(result);
        }

        /// <summary>
        /// Returns fleet-level device statistics: operational status, gateway load, active alerts, firmware drift.
        /// </summary>
        [HttpGet("~/api/v1/device-statistics")]
        [SwaggerOperation(Summary = "Get device statistics", Description = "Returns fleet-level metrics including operational status, gateway load, active alerts, and firmware drift.")]
        [ProducesResponseType(typeof(DeviceKpiDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetStatistics()
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var propertyIds = new List<long>();

            var landlord = await _context.Landlords
                .FirstOrDefaultAsync(l => l.UserId == userId.Value);
            
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
                    .FirstOrDefaultAsync(t => t.UserId == userId.Value);
                if (tenant != null && tenant.PropertyId.HasValue)
                {
                    propertyIds.Add(tenant.PropertyId.Value);
                }
                else
                {
                    return Ok(new DeviceKpiDto("100%", "0.00", "0", "0"));
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

            var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            var deviceIds = devices.Select(d => d.Id).ToList();
            var msgCount = await _context.TelemetryLogs
                .CountAsync(t => deviceIds.Contains(t.DeviceId) && t.Timestamp >= oneMinuteAgo);

            var load = Math.Round((double)msgCount / 60.0, 2);

            var activeAlertsCount = await _context.Alerts
                .CountAsync(a => deviceIds.Contains(a.DeviceId));

            return Ok(new DeviceKpiDto(
                opStatus,
                load.ToString("F2"),
                activeAlertsCount.ToString(),
                outdated.ToString()
            ));
        }

        /// <summary>
        /// Registers a new device or claims an existing unassigned device.
        /// </summary>
        [HttpPost]
        [SwaggerOperation(Summary = "Register a device", Description = "Registers a new IoT device or claims an unassigned device from the pool.")]
        [ProducesResponseType(typeof(DeviceResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(DeviceResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Id))
                return BadRequest(new ErrorResponse("BadRequest", _localizer["Device_IdRequired"]));
            
            var existing = await _context.Devices.FirstOrDefaultAsync(d => d.Id == request.Id);
            if (existing != null)
            {
                if (existing.PropertyId != null)
                {
                    return BadRequest(new ErrorResponse("BadRequest", _localizer["Device_AlreadyRegistered"]));
                }

                if (request.PropertyId.HasValue)
                {
                    var propertyExists = await _context.Properties.AnyAsync(p => p.Id == request.PropertyId.Value);
                    if (!propertyExists)
                        return BadRequest(new ErrorResponse("BadRequest", _localizer["Property_TargetNotFound"]));
                    existing.AssignToProperty(request.PropertyId.Value);
                }

                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    existing.UpdateName(request.Name);
                }

                if (!string.IsNullOrWhiteSpace(request.MacAddress))
                {
                    var existingMac = await _context.Devices.AnyAsync(d => d.MacAddress == request.MacAddress && d.Id != existing.Id);
                    if (existingMac)
                        return BadRequest(new ErrorResponse("BadRequest", _localizer["Device_MacAlreadyRegistered"]));
                    existing.UpdateMacAddress(request.MacAddress);
                }

                await _context.SaveChangesAsync();
                return Ok(MapToDeviceResponse(existing));
            }

            if (!string.IsNullOrWhiteSpace(request.MacAddress))
            {
                var existingMac = await _context.Devices.AnyAsync(d => d.MacAddress == request.MacAddress);
                if (existingMac)
                    return BadRequest(new ErrorResponse("BadRequest", _localizer["Device_MacAlreadyRegistered"]));
            }
            
            var device = new Nexora.Domain.Entities.Device(request.Id, ConnectionStatus.Online, DateTime.UtcNow, request.MacAddress, request.Name);
            if (request.PropertyId.HasValue)
            {
                var propertyExists = await _context.Properties.AnyAsync(p => p.Id == request.PropertyId.Value);
                if (!propertyExists)
                    return BadRequest(new ErrorResponse("BadRequest", _localizer["Property_TargetNotFound"]));
                device.AssignToProperty(request.PropertyId.Value);
            }
            
            _context.Devices.Add(device);
            await _context.SaveChangesAsync();
            return StatusCode(201, MapToDeviceResponse(device));
        }

        /// <summary>
        /// Returns detailed information for a specific device.
        /// </summary>
        [HttpGet("{deviceId}")]
        [SwaggerOperation(Summary = "Get device by ID", Description = "Returns detailed information for a specific device including latest telemetry and valve state.")]
        [ProducesResponseType(typeof(DeviceListItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(string deviceId)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var device = await _context.Devices
                .Include(d => d.Property)
                .FirstOrDefaultAsync(d => d.Id == deviceId);

            if (device == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Device_NotFound"]));

            var latestReading = await _context.TelemetryLogs
                .Where(t => t.DeviceId == deviceId)
                .OrderByDescending(t => t.Timestamp)
                .Select(t => deviceId.Contains("gas") ? Math.Round(t.GasReading, 3).ToString() + " ppm" :
                             deviceId.Contains("water") ? Math.Round(t.WaterReading, 3).ToString() + " L/min" :
                             deviceId.Contains("volt") ? (t.VoltageOk ? "220 V (Normal)" : "Low Voltage") :
                             Math.Round(t.ElectricityReading, 3).ToString() + " A")
                .FirstOrDefaultAsync() ?? (device.ConnectionStatus == ConnectionStatus.Online ? "Active" : "Offline");

            var result = new DeviceListItemDto(
                device.Id,
                device.Name ?? "Unknown",
                device.MacAddress,
                device.Rssi,
                device.FirmwareVersion,
                device.FirmwareVersion != null && device.FirmwareVersion != "v2.4.1",
                device.ConnectionStatus.ToString(),
                device.LastSyncAt,
                device.PropertyId,
                device.Property?.Name ?? "Unassigned",
                latestReading,
                DeviceCommandQueue.ValveStates.TryGetValue(device.Id, out var state) ? state : "OPEN"
            );

            return Ok(result);
        }

        /// <summary>
        /// Partially updates a device (e.g., reassign to a different property).
        /// </summary>
        [HttpPatch("{deviceId}")]
        [SwaggerOperation(Summary = "Update a device", Description = "Partially updates a device. Use to reassign to a different property or update metadata.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateDevice(string deviceId, [FromBody] AssignDeviceRequest request)
        {
            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);
            if (device == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Device_NotFound"]));

            if (request.PropertyId.HasValue)
            {
                var propertyExists = await _context.Properties.AnyAsync(p => p.Id == request.PropertyId.Value);
                if (!propertyExists)
                    return BadRequest(new ErrorResponse("BadRequest", _localizer["Property_TargetNotFound"]));
                device.AssignToProperty(request.PropertyId.Value);
            }
            else
            {
                device.AssignToProperty(null);
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// Deletes a device.
        /// </summary>
        [HttpDelete("{deviceId}")]
        [SwaggerOperation(Summary = "Delete a device", Description = "Removes a device record.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteDevice(string deviceId)
        {
            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);
            if (device == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Device_NotFound"]));

            _context.Devices.Remove(device);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// Sends a command to a device (e.g., CLOSE_VALVE, OPEN_VALVE, REBOOT).
        /// The command is queued and delivered on the next telemetry heartbeat.
        /// </summary>
        [HttpPost("{deviceId}/commands")]
        [SwaggerOperation(Summary = "Send command to device", Description = "Queues a command for the device. Supported: CLOSE_VALVE, OPEN_VALVE, REBOOT.")]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SendCommand(string deviceId, [FromBody] DeviceCommandRequest request)
        {
            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);
            if (device == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Device_NotFound"]));

            DeviceCommandQueue.PendingCommands[deviceId] = request.Command;

            if (request.Command == "CLOSE_VALVE")
            {
                DeviceCommandQueue.ValveStates[deviceId] = "CLOSED";
            }
            else if (request.Command == "OPEN_VALVE")
            {
                DeviceCommandQueue.ValveStates[deviceId] = "OPEN";
            }
            else if (request.Command == "REBOOT")
            {
                device.UpdateSync(ConnectionStatus.Offline, DateTime.UtcNow);
                await _context.SaveChangesAsync();
            }

            return Ok(new MessageResponse($"Command {request.Command} successfully queued for device {deviceId}."));
        }

        /// <summary>
        /// Returns the command history for a specific device (placeholder — commands are not persisted yet).
        /// </summary>
        [HttpGet("{deviceId}/commands")]
        [SwaggerOperation(Summary = "Get device command history", Description = "Returns the command history for a device. Currently returns an empty list.")]
        [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCommands(string deviceId)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);
            if (device == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Device_NotFound"]));

            return Ok(new List<object>());
        }

        private static DeviceResponseDto MapToDeviceResponse(Nexora.Domain.Entities.Device d)
        {
            return new DeviceResponseDto(
                d.Id,
                d.Name,
                d.MacAddress,
                d.ConnectionStatus.ToString(),
                d.FirmwareVersion,
                d.PropertyId,
                d.LastSyncAt
            );
        }
    }

    public record AssignDeviceRequest(long? PropertyId);
    public record RegisterDeviceRequest(string Id, string? Name, long? PropertyId, string? MacAddress);
    public record DeviceCommandRequest(string Command);
}
