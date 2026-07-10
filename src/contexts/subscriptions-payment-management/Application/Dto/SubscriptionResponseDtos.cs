using System.ComponentModel;

namespace Nexora.Application.Dto
{
    /// <summary>
    /// Response for retrieving the current subscription.
    /// Subscription will be null if no active subscription exists.
    /// </summary>
    public record CurrentSubscriptionResponseDto(
        [property: Description("Current subscription details, or null if no active subscription")] SubscriptionDto? Subscription,
        [property: Description("Status message when no subscription was found")] string? Message
    );

    /// <summary>
    /// Response for subscription sync operations (e.g., after returning from Stripe Checkout).
    /// </summary>
    public record SyncSubscriptionResponseDto(
        [property: Description("Synchronized subscription details, or null if not found")] SubscriptionDto? Subscription
    );

    /// <summary>
    /// Response after cancelling a subscription.
    /// The subscription remains active until the current billing period ends.
    /// </summary>
    public record CancelSubscriptionResponseDto(
        [property: Description("Confirmation message")] string Message,
        [property: Description("Updated subscription details with cancel-at-period-end flag")] SubscriptionDto Subscription
    );

    /// <summary>
    /// Response after resuming a previously cancelled subscription.
    /// </summary>
    public record ResumeSubscriptionResponseDto(
        [property: Description("Confirmation message")] string Message,
        [property: Description("Updated subscription details")] SubscriptionDto Subscription
    );

    /// <summary>
    /// Response after cancelling the current subscription (local-only flow).
    /// </summary>
    public record CancelCurrentResponseDto(
        [property: Description("Confirmation message")] string Message,
        [property: Description("UTC timestamp when the subscription period ends")] DateTime CurrentPeriodEnd
    );

    /// <summary>
    /// Single payment method response wrapper.
    /// </summary>
    public record PaymentMethodResponseDto(
        [property: Description("Payment method details, or null if no card is saved")] PaymentMethodDto? PaymentMethod
    );

    /// <summary>
    /// Multiple payment methods response wrapper.
    /// </summary>
    public record PaymentMethodsResponseDto(
        [property: Description("List of saved payment methods")] List<PaymentMethodDto> PaymentMethods
    );

    /// <summary>
    /// Invoice list response wrapper.
    /// </summary>
    public record InvoicesResponseDto(
        [property: Description("List of invoices for the current subscription")] List<InvoiceDto> Invoices
    );

    /// <summary>
    /// Stripe webhook error response.
    /// </summary>
    public record StripeErrorResponse(
        [property: Description("Error message from Stripe")] string Message
    );
}
