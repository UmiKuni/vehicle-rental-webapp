using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;

namespace CarRental.Modules.Payments.Services.Gateways;

/// <summary>
/// PayPal payment gateway integration (expects Payments:PayPal:* configuration: ClientId, ClientSecret, WebhookId, BaseUrl).
/// </summary>
public sealed class PayPalPaymentGateway : IPaymentGatewayProvider
{
    public PaymentMethod Method => PaymentMethod.Paypal;

    public (string PaymentUrl, string QrCodeUrl, DateTimeOffset ExpiresAt) CreateCheckoutSession(
        Guid paymentId,
        Guid bookingId,
        decimal amount,
        string returnUrl,
        DateTimeOffset now)
    {
        throw new NotImplementedException("PayPal Orders API checkout session creation is not yet implemented.");
    }

    public bool VerifyWebhookSignature(PaymentWebhookRequest request)
    {
        throw new NotImplementedException("PayPal webhook signature verification is not yet implemented.");
    }

    public Task<Result<GatewayRefundResult>> RefundAsync(
        Guid paymentId,
        string gatewayTransactionId,
        decimal amount,
        string reason,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("PayPal captures refund API integration is not yet implemented.");
    }
}
