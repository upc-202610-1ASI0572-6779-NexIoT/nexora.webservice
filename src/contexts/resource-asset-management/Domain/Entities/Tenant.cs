using System;

namespace Nexora.Domain.Entities
{
    public class Tenant
    {
        public long Id { get; private set; }
        public long? PropertyId { get; private set; }
        public Property? Property { get; private set; }

        public long? UserId { get; private set; }
        public User? User { get; private set; }

        public string FirstName { get; private set; } = null!;
        public string LastName { get; private set; } = null!;
        public string Country { get; private set; } = null!;
        public string City { get; private set; } = null!;
        public string Address { get; private set; } = null!;
        public string? PhoneNumber { get; private set; }

        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        #pragma warning disable CS8618
        private Tenant() { }
        #pragma warning restore CS8618

        public Tenant(string firstName, string lastName, string country, string city, string address, string? phoneNumber = null, long? userId = null, long? propertyId = null)
        {
            if (string.IsNullOrWhiteSpace(firstName)) throw new ArgumentException("First name is required.");
            if (string.IsNullOrWhiteSpace(lastName)) throw new ArgumentException("Last name is required.");
            if (string.IsNullOrWhiteSpace(country)) throw new ArgumentException("Country is required.");
            if (string.IsNullOrWhiteSpace(city)) throw new ArgumentException("City is required.");
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Address is required.");

            PropertyId = propertyId;
            FirstName = firstName;
            LastName = lastName;
            Country = country;
            City = city;
            Address = address;
            PhoneNumber = phoneNumber;
            UserId = userId;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdatePersonalInfo(string firstName, string lastName, string country, string city, string address, string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(firstName)) throw new ArgumentException("First name is required.");
            if (string.IsNullOrWhiteSpace(lastName)) throw new ArgumentException("Last name is required.");
            if (string.IsNullOrWhiteSpace(country)) throw new ArgumentException("Country is required.");
            if (string.IsNullOrWhiteSpace(city)) throw new ArgumentException("City is required.");
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Address is required.");

            FirstName = firstName;
            LastName = lastName;
            Country = country;
            City = city;
            Address = address;
            PhoneNumber = phoneNumber;
            UpdatedAt = DateTime.UtcNow;
        }

        public void LinkUser(long userId)
        {
            UserId = userId;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
