using System.Collections.Generic;

namespace Nexora.Application.Dto
{
    public record SubscriptionPlanDto(
        long Id,
        string Name,
        decimal MonthlyPrice,
        int MaxPropertiesLimit,
        bool UnlimitedProperties,
        string? Tagline = null,
        string? Description = null,
        IReadOnlyList<string>? Features = null,
        bool IsPopular = false,
        string? TargetUser = null
    );

    /// <summary>Response for creating a Stripe Checkout Session (hosted payment page).</summary>
    public record CheckoutSessionResponse(string Url, string SessionId);

    /// <summary>Public Stripe configuration needed by the client SDK (flutter_stripe).</summary>
    public record StripeConfigDto(string PublishableKey);

    public record SubscriptionDto(
        long Id,
        SubscriptionPlanDto Plan,
        string Status,
        DateTime StartedAt,
        DateTime CurrentPeriodStart,
        DateTime CurrentPeriodEnd,
        bool CancelAtPeriodEnd
    );

    public record ActivateSubscriptionRequest(
        long SubscriptionPlanId,
        string? Brand = null,
        string? FullNumber = null,
        string? ExpiryMonth = null,
        string? ExpiryYear = null,
        string? HolderName = null,
        string? Cvv = null,
        string? PaymentMethodId = null
    );

    public record ActivateSubscriptionResponse(
        SubscriptionDto Subscription,
        decimal AmountDue,
        DateTime DueDate,
        long InvoiceId,
        string? ClientSecret = null
    );

    public record PaymentMethodDto(
        long Id,
        string Brand,
        string LastFour,
        string FullNumber,
        string ExpiryMonth,
        string ExpiryYear,
        string HolderName,
        string Cvv,
        string FirstName,
        string LastName
    );

    public record InvoiceDto(
        long Id,
        decimal Amount,
        string Status,
        DateTime DueDate,
        DateTime CreatedAt
    );

    public record PaymentMethodDetailDto(
        long Id,
        string Brand,
        string LastFour,
        string FullNumber,
        string ExpiryMonth,
        string ExpiryYear,
        string HolderName,
        string Cvv
    );

    public record UpdatePaymentMethodRequest(
        string? Brand = null,
        string? FullNumber = null,
        string? ExpiryMonth = null,
        string? ExpiryYear = null,
        string? HolderName = null,
        string? Cvv = null,
        string? PaymentMethodId = null
    );
}
