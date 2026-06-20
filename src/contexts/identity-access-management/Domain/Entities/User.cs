using System;
using System.Collections.Generic;

namespace Nexora.Domain.Entities
{
    public class User
    {
        public long Id { get; private set; }
        public string Email { get; private set; } = null!;
        public string PasswordHash { get; private set; } = null!;
        public bool IsActive { get; private set; }
        public int FailedLoginAttempts { get; private set; }
        public DateTime? LockedAt { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }


        #pragma warning disable CS8618
        private User() { }
        #pragma warning restore CS8618

        public User(string email, string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.");
            
            Email = email;
            PasswordHash = passwordHash;
            IsActive = true;
            FailedLoginAttempts = 0;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdatePassword(string newHash)
        {
            PasswordHash = newHash;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
