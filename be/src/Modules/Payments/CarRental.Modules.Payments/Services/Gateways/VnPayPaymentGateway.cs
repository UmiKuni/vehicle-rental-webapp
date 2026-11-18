using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;

namespace CarRental.Modules.Payments.Services.Gateways;

/// <summary>
/// VNPay payment gateway integration (expects Payments:VNPay:* configuration: TmnCode, HashSecret, BaseUrl).
/// </summary>
public sealed class VnPayPaymentGateway : IPaymentGatewayProvider
{
    public PaymentMethod Method => PaymentMethod.VNPay;

    public (string PaymentUrl, string QrCodeUrl, DateTimeOffset ExpiresAt) CreateCheckoutSession(
        Guid paymentId,
        Guid bookingId,
        decimal amount,
        string returnUrl,
        DateTimeOffset now)
    {
        throw new NotImplementedException("VNPay checkout session creation is not yet implemented.");
    }

    public bool VerifyWebhookSignature(PaymentWebhookRequest request)
    {
        throw new NotImplementedException("VNPay IPN HMAC-SHA512 signature verification is not yet implemented.");
    }

    public Task<Result<GatewayRefundResult>> RefundAsync(
        Guid paymentId,
        string gatewayTransactionId,
        decimal amount,
        string reason,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("VNPay refund API integration is not yet implemented.");
    }
}
