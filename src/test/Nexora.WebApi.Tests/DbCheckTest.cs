using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Nexora.WebApi.Tests
{
    public class DbCheckTest
    {
        private readonly ITestOutputHelper _output;

        public DbCheckTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void DumpDatabase()
        {
            var optionsBuilder = new DbContextOptionsBuilder<NexoraDbContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Database=nexora_db;Username=postgres;Password=12345678");

            using (var context = new NexoraDbContext(optionsBuilder.Options))
            {
                _output.WriteLine("=== PROPERTIES ===");
                var properties = context.Properties.ToList();
                foreach (var p in properties)
                {
                    _output.WriteLine($"Id={p.Id}, Name={p.Name}, LandlordId={p.LandlordId}");
                }

                _output.WriteLine("\n=== DEVICES ===");
                var devices = context.Devices.ToList();
                foreach (var d in devices)
                {
                    _output.WriteLine($"Id={d.Id}, Name={d.Name}, PropertyId={d.PropertyId}, Status={d.ConnectionStatus}");
                }

                _output.WriteLine("\n=== TELEMETRIES ===");
                var telemetries = context.TelemetryLogs.OrderByDescending(t => t.Timestamp).Take(10).ToList();
                foreach (var t in telemetries)
                {
                    _output.WriteLine($"DeviceId={t.DeviceId}, Time={t.Timestamp}, Gas={t.GasReading}, Water={t.WaterReading}, Electricity={t.ElectricityReading}");
                }
                
                _output.WriteLine("\n=== ALERTS ===");
                var alerts = context.Alerts.OrderByDescending(a => a.Timestamp).Take(10).ToList();
                foreach (var a in alerts)
                {
                    _output.WriteLine($"DeviceId={a.DeviceId}, Time={a.Timestamp}, Type={a.Type}, Severity={a.Severity}");
                }
            }
        }
    }
}
