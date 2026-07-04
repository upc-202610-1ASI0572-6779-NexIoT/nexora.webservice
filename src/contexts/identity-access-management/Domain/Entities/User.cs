using System;

namespace Nexora.Domain.Entities
{
    public class User
    {
        public const string LandlordType = "Landlord";
        public const string TenantType = "Tenant";

        public long Id { get; private set; }
        public string Email { get; private set; } = null!;
        public string PasswordHash { get; private set; } = null!;
        public bool IsActive { get; private set; }
        public int FailedLoginAttempts { get; private set; }
        public DateTime? LockedAt { get; private set; }
        public string? UserableType { get; private set; }
        public long? UserableId { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        #pragma warning disable CS8618
        private User() { }
        #pragma warning restore CS8618

        public User(string email, string passwordHash, string userableType)
        {
            if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.");
            if (string.IsNullOrWhiteSpace(userableType)) throw new ArgumentException("Userable type is required.");
            if (userableType != LandlordType && userableType != TenantType)
                throw new ArgumentException("Invalid userable type. Must be 'Landlord' or 'Tenant'.");

            Email = email;
            PasswordHash = passwordHash;
            UserableType = userableType;
            IsActive = true;
            FailedLoginAttempts = 0;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetUserableProfile(long profileId)
        {
            UserableId = profileId;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdatePassword(string newHash)
        {
            PasswordHash = newHash;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
