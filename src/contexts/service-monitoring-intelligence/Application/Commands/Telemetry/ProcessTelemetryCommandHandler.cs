using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Nexora.Domain.Repositories;

namespace Nexora.Application.Commands.Telemetry
{
    public class ProcessTelemetryCommandHandler : IRequestHandler<ProcessTelemetryCommand>
    {
        private readonly IDeviceRepository _deviceRepository;
        private readonly ITelemetryLogRepository _telemetryLogRepository;
        private readonly IAlertRepository _alertRepository;
        private readonly IPropertyRepository _propertyRepository;
        private readonly IMaintenanceTicketRepository _maintenanceTicketRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ProcessTelemetryCommandHandler(
            IDeviceRepository deviceRepository,
            ITelemetryLogRepository telemetryLogRepository,
            IAlertRepository alertRepository,
            IPropertyRepository propertyRepository,
            IMaintenanceTicketRepository maintenanceTicketRepository,
            IUnitOfWork unitOfWork)
        {
            _deviceRepository = deviceRepository;
            _telemetryLogRepository = telemetryLogRepository;
            _alertRepository = alertRepository;
            _propertyRepository = propertyRepository;
            _maintenanceTicketRepository = maintenanceTicketRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(ProcessTelemetryCommand request, CancellationToken cancellationToken)
        {
            var payload = request.Payload;

            // Convert Unix timestamp to UTC DateTime
            var syncDateTime = DateTimeOffset.FromUnixTimeSeconds(payload.Timestamp).UtcDateTime;

            // Explicit database transaction block to guarantee all-or-nothing atomicity
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // 1. Fetch or automatically register Device
                var device = await _deviceRepository.GetByIdAsync(payload.DeviceId);
                if (device == null)
                {
                    device = new Device(payload.DeviceId, ConnectionStatus.Online, syncDateTime);
                    await _deviceRepository.AddAsync(device);
                }
                else
                {
                    device.UpdateSync(ConnectionStatus.Online, syncDateTime);
                    await _deviceRepository.UpdateAsync(device);
                }

                // 2. Create and persist TelemetryLog (immutable)
                var telemetryLog = new TelemetryLog(
                    payload.DeviceId,
                    payload.Sensors.WaterLpm,
                    payload.Sensors.GasPpm,
                    payload.Sensors.Presence == 1,
                    payload.Sensors.ElectricityKwh,
                    payload.Sensors.VoltageOk,
                    syncDateTime
                );

                await _telemetryLogRepository.AddAsync(telemetryLog);

                // 3. Evaluate GasPpm rules for alerts:
                if (payload.Sensors.GasPpm > 100)
                {
                    var severity = payload.Sensors.GasPpm > 300 
                        ? AlertSeverity.Critical 
                        : AlertSeverity.Warning;

                    var alert = new Alert(
                        severity,
                        "Gas Threshold Exceeded",
                        syncDateTime,
                        payload.DeviceId
                    );

                    await _alertRepository.AddAsync(alert);
                }

                // 4. Evaluate Security Mode rule:
                // Presence == 1 AND the Property associated with Device has IsSecurityModeArmed == true
                if (payload.Sensors.Presence == 1)
                {
                    var property = await _propertyRepository.GetByDeviceIdAsync(payload.DeviceId);
                    if (property != null && property.IsSecurityModeArmed)
                    {
                        // Create intrusion alert with type 'Intrusión' and 'Critical' severity
                        var intrusionAlert = new Alert(
                            AlertSeverity.Critical,
                            "Intrusión",
                            syncDateTime,
                            payload.DeviceId
                        );
                        await _alertRepository.AddAsync(intrusionAlert);

                        // Simultaneously generate an automatic maintenance ticket linked to the critical intrusion alert
                        var ticket = new MaintenanceTicket(intrusionAlert);
                        await _maintenanceTicketRepository.AddAsync(ticket);
                    }
                }

                // 5. Evaluate Overcurrent rule:
                if (payload.Sensors.ElectricityKwh > 20.0)
                {
                    var alert = new Alert(
                        AlertSeverity.Warning,
                        "Overcurrent Detected",
                        syncDateTime,
                        payload.DeviceId
                    );
                    await _alertRepository.AddAsync(alert);
                }

                // 6. Evaluate Voltage instability rule:
                if (!payload.Sensors.VoltageOk)
                {
                    var alert = new Alert(
                        AlertSeverity.Critical,
                        "Voltage Instability Detected",
                        syncDateTime,
                        payload.DeviceId
                    );
                    await _alertRepository.AddAsync(alert);
                }

                // 7. Evaluate Water Leak / Waste rule:
                if (payload.Sensors.WaterLpm > 0.0)
                {
                    var flowStart = await _telemetryLogRepository.GetContinuousFlowStartTimeAsync(payload.DeviceId);
                    DateTime start = flowStart ?? syncDateTime;
                    double secondsFlowing = (syncDateTime - start).TotalSeconds;

                    if (secondsFlowing >= 15.0)
                    {
                        bool hasAlert = await _alertRepository.HasActiveAlertAsync(payload.DeviceId, "Water Leak Detected");
                        if (!hasAlert)
                        {
                            var alert = new Alert(
                                AlertSeverity.Critical,
                                "Water Leak Detected",
                                syncDateTime,
                                payload.DeviceId
                            );
                            await _alertRepository.AddAsync(alert);
                        }
                    }
                }

                // Commit the transaction atomically
                await _unitOfWork.CommitTransactionAsync();
            }
            catch (Exception)
            {
                // Rollback the transaction in case of any database or validation errors
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }
    }
}
