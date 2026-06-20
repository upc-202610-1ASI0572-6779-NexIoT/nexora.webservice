using System;
using System.Collections.Generic;

namespace Nexora.Domain.Entities
{
    public class Landlord
    {
        public long Id { get; private set; }
        public long UserId { get; private set; }
        public User User { get; private set; } = null!;
        
        public string FirstName { get; private set; } = null!;
        public string LastName { get; private set; } = null!;
        public string Country { get; private set; } = null!;
        public string City { get; private set; } = null!;
        public string Address { get; private set; } = null!;
        public string? PhoneNumber { get; private set; }
        
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public ICollection<Property> Properties { get; private set; } = new List<Property>();

        #pragma warning disable CS8618
        private Landlord() { }
        #pragma warning restore CS8618

        public Landlord(long userId, string firstName, string lastName, string country, string city, string address, string? phoneNumber = null)
        {
            UserId = userId;
            FirstName = firstName;
            LastName = lastName;
            Country = country;
            City = city;
            Address = address;
            PhoneNumber = phoneNumber;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdatePersonalInfo(string firstName, string lastName, string country, string city, string address, string? phoneNumber)
        {
            FirstName = firstName;
            LastName = lastName;
            Country = country;
            City = city;
            Address = address;
            PhoneNumber = phoneNumber;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
