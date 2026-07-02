using System;

namespace Nexora.Domain.Entities
{
    public class SavedCard
    {
        public long Id { get; private set; }

        public long LandlordId { get; private set; }
        public Landlord Landlord { get; private set; } = null!;

        public string Brand { get; private set; } = null!;
        public string LastFour { get; private set; } = null!;
        public string FullNumber { get; private set; } = null!;
        public string ExpiryMonth { get; private set; } = null!;
        public string ExpiryYear { get; private set; } = null!;
        public string HolderName { get; private set; } = null!;
        public string Cvv { get; private set; } = null!;
        public bool IsDefault { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private SavedCard() { }

        public SavedCard(long landlordId, string brand, string fullNumber, string expiryMonth, string expiryYear, string holderName, string cvv, bool isDefault = true)
        {
            LandlordId = landlordId;
            Brand = brand;
            FullNumber = fullNumber;
            LastFour = fullNumber.Length >= 4 ? fullNumber[^4..] : fullNumber;
            ExpiryMonth = expiryMonth;
            ExpiryYear = expiryYear;
            HolderName = holderName;
            Cvv = cvv;
            IsDefault = isDefault;
            CreatedAt = DateTime.UtcNow;
        }

        public void SetCreatedAt(DateTime createdAt)
        {
            CreatedAt = createdAt;
        }

        public void Update(string? brand, string? fullNumber, string? expiryMonth, string? expiryYear, string? holderName, string? cvv)
        {
            if (!string.IsNullOrEmpty(brand)) Brand = brand;
            if (!string.IsNullOrEmpty(fullNumber))
            {
                FullNumber = fullNumber;
                LastFour = fullNumber.Length >= 4 ? fullNumber[^4..] : fullNumber;
            }
            if (!string.IsNullOrEmpty(expiryMonth)) ExpiryMonth = expiryMonth;
            if (!string.IsNullOrEmpty(expiryYear)) ExpiryYear = expiryYear;
            if (!string.IsNullOrEmpty(holderName)) HolderName = holderName;
            if (!string.IsNullOrEmpty(cvv)) Cvv = cvv;
        }
    }
}
