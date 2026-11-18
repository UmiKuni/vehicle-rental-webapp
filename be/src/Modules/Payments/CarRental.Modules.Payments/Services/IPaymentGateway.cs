using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;

namespace CarRental.Modules.Payments.Services;

public sealed record GatewayRefundResult(
    string RefundTransactionId,
    decimal RefundedAmount,
    DateTimeOffset ProcessedAt);

public interface IPaymentGateway
{
    (string PaymentUrl, string QrCodeUrl, DateTimeOffset ExpiresAt) CreateCheckoutSession(
        Guid paymentId,
        Guid bookingId,
        decimal amount,
        PaymentMethod method,
        string returnUrl,
        DateTimeOffset now);

    bool VerifyWebhookSignature(PaymentWebhookRequest request);

    Task<Result<GatewayRefundResult>> RefundAsync(
        Guid paymentId,
        PaymentMethod method,
        string gatewayTransactionId,
        decimal amount,
        string reason,
        CancellationToken ct = default);
}
