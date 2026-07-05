using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexora.Domain.Enums;
using Nexora.Domain.Services;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Services
{
    public class PropertyCodeGenerator : IPropertyCodeGenerator
    {
        private readonly NexoraDbContext _context;

        public PropertyCodeGenerator(NexoraDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateAsync(PropertyType type)
        {
            var prefix = type switch
            {
                PropertyType.HOUSE => "HSE-",
                PropertyType.APARTMENT => "APT-",
                PropertyType.ROOM => "ROM-",
                PropertyType.OFFICE => "OFC-",
                PropertyType.COMMERCIAL => "COM-",
                _ => "PRP-"
            };

            // Find the highest existing suffix for this type prefix
            var existingCodes = await _context.Properties
                .Where(p => p.PropertyCode != null && p.PropertyCode.StartsWith(prefix))
                .Select(p => p.PropertyCode)
                .ToListAsync();

            var maxSuffix = 0;
            foreach (var code in existingCodes)
            {
                var suffixPart = code[prefix.Length..];
                if (int.TryParse(suffixPart, out var num))
                {
                    if (num > maxSuffix) maxSuffix = num;
                }
            }

            var nextSuffix = maxSuffix + 1;

            // Format with at least 3 digits, more if needed beyond 999
            var suffix = nextSuffix.ToString().PadLeft(Math.Max(3, nextSuffix.ToString().Length), '0');

            return prefix + suffix;
        }
    }
}
