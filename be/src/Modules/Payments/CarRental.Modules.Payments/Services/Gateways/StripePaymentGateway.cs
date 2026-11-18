using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;

namespace CarRental.Modules.Payments.Services.Gateways;

/// <summary>
/// Stripe payment gateway integration (expects Payments:Stripe:* configuration: SecretKey, PublishableKey, WebhookSecret).
/// </summary>
public sealed class StripePaymentGateway : IPaymentGatewayProvider
{
    public PaymentMethod Method => PaymentMethod.Stripe;

    public (string PaymentUrl, string QrCodeUrl, DateTimeOffset ExpiresAt) CreateCheckoutSession(
        Guid paymentId,
        Guid bookingId,
        decimal amount,
        string returnUrl,
        DateTimeOffset now)
    {
        throw new NotImplementedException("Stripe Checkout Session creation is not yet implemented.");
    }

    public bool VerifyWebhookSignature(PaymentWebhookRequest request)
    {
        throw new NotImplementedException("Stripe webhook signature verification is not yet implemented.");
    }

    public Task<Result<GatewayRefundResult>> RefundAsync(
        Guid paymentId,
        string gatewayTransactionId,
        decimal amount,
        string reason,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("Stripe Refunds API integration is not yet implemented.");
    }
}
