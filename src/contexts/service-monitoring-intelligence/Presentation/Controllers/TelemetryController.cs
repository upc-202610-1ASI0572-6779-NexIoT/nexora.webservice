using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Nexora.Application.Dto;
using Nexora.Application.Services;
using Nexora.Infrastructure.Persistence;
using Nexora.Shared.Domain.Api;
using Nexora.Shared.Infrastructure;
using Nexora.Shared.Domain.Resources;
using Swashbuckle.AspNetCore.Annotations;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/telemetry-records")]
    [SwaggerTag("Telemetry")]
    public class TelemetryController : ControllerBase
    {
        private readonly ITelemetryProcessor _telemetryProcessor;
        private readonly NexoraDbContext _context;
        private readonly IStringLocalizer<SharedMessages> _localizer;

        public TelemetryController(ITelemetryProcessor telemetryProcessor, NexoraDbContext context, IStringLocalizer<SharedMessages> localizer)
        {
            _telemetryProcessor = telemetryProcessor;
            _context = context;
            _localizer = localizer;
        }

        /// <summary>
        /// Ingests telemetry data from an IoT device. The device polls this endpoint periodically.
        /// </summary>
        [HttpPost]
        [SwaggerOperation(Summary = "Ingest telemetry data", Description = "Receives sensor readings from an IoT device.")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PostTelemetry([FromBody] TelemetryPayloadDto payload)
        {
            await _telemetryProcessor.ProcessAsync(payload);

            // Autonomous edge mitigation
            if (payload.Sensors != null && (payload.Sensors.GasPpm >= 400.0 || payload.Sensors.WaterLpm >= 30.0))
            {
                DeviceCommandQueue.ValveStates[payload.DeviceId] = "CLOSED";
            }

            return StatusCode(201);
        }

        /// <summary>
        /// Returns telemetry records. Use ?latest=true for the most recent reading per device,
        /// or ?deviceId + date range for historical data.
        /// </summary>
        [Authorize]
        [HttpGet]
        [SwaggerOperation(Summary = "List telemetry records", Description = "Returns telemetry records. Filter by deviceId, date range, or use latest=true for most recent readings.")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetTelemetryRecords(
            [FromQuery] string? deviceId = null,
            [FromQuery] bool? latest = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            if (latest == true)
            {
                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    return BadRequest(new ErrorResponse("BadRequest", _localizer["Device_IdRequired"]));
                }

                var latestRecord = await _context.TelemetryLogs
                    .Where(t => t.DeviceId == deviceId)
                    .OrderByDescending(t => t.Timestamp)
                    .Select(t => new TelemetryLatestDto(
                        t.DeviceId,
                        t.WaterReading,
                        t.GasReading,
                        t.PresenceReading,
                        t.ElectricityReading,
                        t.VoltageOk,
                        t.Timestamp
                    ))
                    .FirstOrDefaultAsync();

                if (latestRecord == null)
                    return NotFound(new ErrorResponse("NotFound", _localizer["Telemetry_NotFound"]));
                return Ok(latestRecord);
            }

            var query = _context.TelemetryLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                query = query.Where(t => t.DeviceId == deviceId);
            }

            if (startDate.HasValue)
            {
                var utcStart = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc);
                query = query.Where(t => t.Timestamp >= utcStart);
            }

            if (endDate.HasValue)
            {
                var utcEnd = DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc);
                query = query.Where(t => t.Timestamp <= utcEnd);
            }

            var records = await query
                .OrderByDescending(t => t.Timestamp)
                .Take(200)
                .Select(t => new TelemetryLatestDto(
                    t.DeviceId,
                    t.WaterReading,
                    t.GasReading,
                    t.PresenceReading,
                    t.ElectricityReading,
                    t.VoltageOk,
                    t.Timestamp
                ))
                .ToListAsync();

            return Ok(records);
        }
    }
}
