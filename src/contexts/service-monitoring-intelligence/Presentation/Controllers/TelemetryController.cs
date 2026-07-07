using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Nexora.Application.Dto;
using Nexora.Application.Services;
using Nexora.Infrastructure.Persistence;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Nexora.Shared.Infrastructure;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/telemetries")]
    public class TelemetryController : ControllerBase
    {
        private readonly ITelemetryProcessor _telemetryProcessor;
        private readonly NexoraDbContext _context;

        public TelemetryController(ITelemetryProcessor telemetryProcessor, NexoraDbContext context)
        {
            _telemetryProcessor = telemetryProcessor;
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> PostTelemetry([FromBody] TelemetryPayloadDto payload)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            System.Console.WriteLine($"\n[CLOUD TELEMETRY] Ingesting from Device: '{payload.DeviceId}'");
            System.Console.WriteLine($"[CLOUD TELEMETRY] Pending commands list: {string.Join(", ", DeviceCommandQueue.PendingCommands.Keys)}");

            await _telemetryProcessor.ProcessAsync(payload);

            // Fetch any pending command for this device
            string valveCommand = "NONE";
            if (DeviceCommandQueue.PendingCommands.TryRemove(payload.DeviceId, out var cmd))
            {
                System.Console.WriteLine($"[CLOUD TELEMETRY] Found and dequeued pending command: '{cmd}' for device '{payload.DeviceId}'");
                if (cmd == "CLOSE_VALVE")
                {
                    valveCommand = "CLOSE";
                    DeviceCommandQueue.ValveStates[payload.DeviceId] = "CLOSED";
                }
                else if (cmd == "OPEN_VALVE")
                {
                    valveCommand = "OPEN";
                    DeviceCommandQueue.ValveStates[payload.DeviceId] = "OPEN";
                }
            }
            else
            {
                System.Console.WriteLine($"[CLOUD TELEMETRY] No pending command found for device '{payload.DeviceId}'");
            }

            // Track autonomous edge mitigation: if sensor reports a leak threshold, mark it closed locally
            if (payload.Sensors != null && (payload.Sensors.GasPpm >= 400.0 || payload.Sensors.WaterLpm >= 30.0))
            {
                DeviceCommandQueue.ValveStates[payload.DeviceId] = "CLOSED";
            }

            System.Console.WriteLine($"[CLOUD TELEMETRY] Responding with valve_command: '{valveCommand}'");
            return StatusCode(201, new { valve_command = valveCommand });
        }

        [Authorize]
        [HttpGet("latest")]
        public async Task<IActionResult> GetLatest([FromQuery] string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return BadRequest("DeviceId is required.");
            }

            var latest = await _context.TelemetryLogs
                .Where(t => t.DeviceId == deviceId)
                .OrderByDescending(t => t.Timestamp)
                .Select(t => new {
                    t.DeviceId,
                    t.WaterReading,
                    t.GasReading,
                    t.PresenceReading,
                    t.ElectricityReading,
                    t.VoltageOk,
                    t.Timestamp
                })
                .FirstOrDefaultAsync();

            if (latest == null) return NotFound();
            return Ok(latest);
        }
    }
}
