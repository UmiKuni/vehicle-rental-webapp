using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;

namespace CarRental.Modules.Payments.Services.Gateways;

/// <summary>
/// Bank transfer / VietQR payment provider (expects Payments:Transfer:* configuration: BankCode, AccountNumber, AccountName, WebhookSecret).
/// </summary>
public sealed class BankTransferPaymentGateway : IPaymentGatewayProvider
{
    public PaymentMethod Method => PaymentMethod.Transfer;

    public (string PaymentUrl, string QrCodeUrl, DateTimeOffset ExpiresAt) CreateCheckoutSession(
        Guid paymentId,
        Guid bookingId,
        decimal amount,
        string returnUrl,
        DateTimeOffset now)
    {
        throw new NotImplementedException("Bank transfer / VietQR session creation is not yet implemented.");
    }

    public bool VerifyWebhookSignature(PaymentWebhookRequest request)
    {
        throw new NotImplementedException("Bank transfer webhook signature verification is not yet implemented.");
    }

    public Task<Result<GatewayRefundResult>> RefundAsync(
        Guid paymentId,
        string gatewayTransactionId,
        decimal amount,
        string reason,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("Bank transfer refund processing is not yet implemented.");
    }
}
