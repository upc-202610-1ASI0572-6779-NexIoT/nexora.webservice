using System;

namespace Nexora.Domain.Entities
{
    public class NotificationPreference
    {
        public long Id { get; private set; }
        public long UserId { get; private set; }
        public User User { get; private set; } = null!;
        public bool ReceiveEmailAlerts { get; private set; }
        public bool ReceiveSmsAlerts { get; private set; }

        private NotificationPreference() { }

        public NotificationPreference(long userId, bool receiveEmailAlerts, bool receiveSmsAlerts)
        {
            UserId = userId;
            ReceiveEmailAlerts = receiveEmailAlerts;
            ReceiveSmsAlerts = receiveSmsAlerts;
        }

        public void UpdatePreferences(bool email, bool sms)
        {
            ReceiveEmailAlerts = email;
            ReceiveSmsAlerts = sms;
        }
    }
}
