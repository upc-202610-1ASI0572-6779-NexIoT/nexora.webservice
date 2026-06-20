using System;
using Nexora.Domain.Enums;

namespace Nexora.Domain.Entities
{
    public class MaintenanceTicket
    {
        public long Id { get; private set; }
        public long AlertId { get; private set; }
        public Alert Alert { get; private set; } = null!;
        public TicketStatus Status { get; private set; }
        public string? AssignedTo { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? ResolvedAt { get; private set; }

        #pragma warning disable CS8618
        private MaintenanceTicket() { }
        #pragma warning restore CS8618

        public MaintenanceTicket(long alertId)
        {
            AlertId = alertId;
            Status = TicketStatus.Pending;
            CreatedAt = DateTime.UtcNow;
        }

        public MaintenanceTicket(Alert alert)
        {
            Alert = alert;
            Status = TicketStatus.Pending;
            CreatedAt = DateTime.UtcNow;
        }

        public void Assign(string technician)
        {
            AssignedTo = technician;
            Status = TicketStatus.Assigned;
        }

        public void Resolve()
        {
            Status = TicketStatus.Resolved;
            ResolvedAt = DateTime.UtcNow;
        }
    }
}
