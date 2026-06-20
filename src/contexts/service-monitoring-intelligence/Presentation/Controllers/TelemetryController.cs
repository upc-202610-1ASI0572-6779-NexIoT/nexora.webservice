using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Nexora.Application.Dto;
using Nexora.Application.Services;
using Nexora.Infrastructure.Persistence;
using System.Linq;
using Microsoft.EntityFrameworkCore;

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

            await _telemetryProcessor.ProcessAsync(payload);

            return StatusCode(201); // Created (HTTP 201)
        }

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
