using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;

namespace CarRental.Modules.Payments.Services;

/// <summary>
/// Gateway-specific provider contract for a single <see cref="PaymentMethod"/>.
/// </summary>
public interface IPaymentGatewayProvider
{
    PaymentMethod Method { get; }

    (string PaymentUrl, string QrCodeUrl, DateTimeOffset ExpiresAt) CreateCheckoutSession(
        Guid paymentId,
        Guid bookingId,
        decimal amount,
        string returnUrl,
        DateTimeOffset now);

    bool VerifyWebhookSignature(PaymentWebhookRequest request);

    Task<Result<GatewayRefundResult>> RefundAsync(
        Guid paymentId,
        string gatewayTransactionId,
        decimal amount,
        string reason,
        CancellationToken ct = default);
}
