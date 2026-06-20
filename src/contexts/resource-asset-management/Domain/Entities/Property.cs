using System;
using System.Collections.Generic;
using Nexora.Domain.Enums;

namespace Nexora.Domain.Entities
{
    public class Property
    {
        public string PropertyCode { get; private set; } = null!;
        public long Id { get; private set; }
        public long LandlordId { get; private set; }
        public Landlord Landlord { get; private set; } = null!;
        
        public string Name { get; private set; } = null!;
        public string? Description { get; private set; }
        public PropertyType PropertyType { get; private set; }
        public string Country { get; private set; } = null!;
        public string City { get; private set; } = null!;
        public string Address { get; private set; } = null!;
        public PropertyStatus Status { get; private set; }
        
        public bool IsSecurityModeArmed { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public ICollection<Tenant> Tenants { get; private set; } = new List<Tenant>();

        #pragma warning disable CS8618
        private Property() { }
        #pragma warning restore CS8618

        public Property(string name, long landlordId, PropertyType type, string country, string city, string address, string propertyCode, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.");
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Address is required.");

            if (string.IsNullOrWhiteSpace(propertyCode)) throw new ArgumentException("PropertyCode is required.");

            Name = name;
            LandlordId = landlordId;
            PropertyType = type;
            Country = country;
            City = city;
            Address = address;
            Description = description;
            PropertyCode = propertyCode;
            Status = PropertyStatus.ACTIVE;
            IsSecurityModeArmed = false;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetPropertyCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("code");
            PropertyCode = code;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateStatus(PropertyStatus newStatus)
        {
            Status = newStatus;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Update(string name, string? description, PropertyType type, string country, string city, string address, PropertyStatus status)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.");
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Address is required.");

            Name = name;
            Description = description;
            PropertyType = type;
            Country = country;
            City = city;
            Address = address;
            Status = status;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetSecurityMode(bool armed)
        {
            IsSecurityModeArmed = armed;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
