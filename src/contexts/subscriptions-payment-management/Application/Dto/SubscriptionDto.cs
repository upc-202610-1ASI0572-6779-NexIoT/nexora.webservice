namespace Nexora.Application.Dto
{
    public record SubscriptionPlanDto(
        long Id,
        string Name,
        decimal MonthlyPrice,
        int MaxPropertiesLimit,
        bool UnlimitedProperties
    );

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
        string? Cvv = null
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
        string ExpiryMonth,
        string ExpiryYear,
        string HolderName,
        string Cvv
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
        string? Brand,
        string? FullNumber,
        string? ExpiryMonth,
        string? ExpiryYear,
        string? HolderName,
        string? Cvv
    );
}
