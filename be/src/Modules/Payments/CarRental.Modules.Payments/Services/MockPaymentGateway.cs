using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;
using Microsoft.Extensions.Configuration;

namespace CarRental.Modules.Payments.Services;

internal sealed class MockPaymentGateway(
    IConfiguration configuration,
    TimeProvider clock) : IPaymentGateway
{
    private static readonly TimeSpan CheckoutTtl = TimeSpan.FromMinutes(15);

    public (string PaymentUrl, string QrCodeUrl, DateTimeOffset ExpiresAt) CreateCheckoutSession(
        Guid paymentId,
        Guid bookingId,
        decimal amount,
        PaymentMethod method,
        string returnUrl,
        DateTimeOffset now)
    {
        var amountVnd = (long)Math.Round(amount, MidpointRounding.AwayFromZero);
        var encodedReturnUrl = Uri.EscapeDataString(returnUrl);
        var expiresAt = now.Add(CheckoutTtl);

        var paymentUrl = method switch
        {
            PaymentMethod.VNPay => FormattableString.Invariant(
                $"https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_TxnRef={paymentId:N}&vnp_Amount={amountVnd * 100}&vnp_ReturnUrl={encodedReturnUrl}"),
            PaymentMethod.Paypal => FormattableString.Invariant(
                $"https://www.sandbox.paypal.com/checkoutnow?token={paymentId:N}&amount={amountVnd}&returnUrl={encodedReturnUrl}"),
            PaymentMethod.Stripe => FormattableString.Invariant(
                $"https://checkout.stripe.com/c/pay/{paymentId:N}?amount={amountVnd}&redirect_url={encodedReturnUrl}"),
            _ => FormattableString.Invariant(
                $"https://sandbox.vietqr.io/pay?orderId={paymentId:N}&bookingId={bookingId:N}&amount={amountVnd}&returnUrl={encodedReturnUrl}")
        };

        var qrPayload = FormattableString.Invariant(
            $"00020101021238580010A000000727|ref={paymentId:N}|amount={amountVnd}");
        var qrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=250x250&data={Uri.EscapeDataString(qrPayload)}";

        return (paymentUrl, qrCodeUrl, expiresAt);
    }

    public bool VerifyWebhookSignature(PaymentWebhookRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Signature))
            return false;

        var configuredSecret = configuration["Payments:WebhookSecret"];
        if (!string.IsNullOrWhiteSpace(configuredSecret))
            return string.Equals(request.Signature.Trim(), configuredSecret.Trim(), StringComparison.Ordinal);

        return !string.Equals(request.Signature.Trim(), "INVALID", StringComparison.OrdinalIgnoreCase);
    }

    public Task<Result<GatewayRefundResult>> RefundAsync(
        Guid paymentId,
        PaymentMethod method,
        string gatewayTransactionId,
        decimal amount,
        string reason,
        CancellationToken ct = default)
    {
        var refundTxnId = $"REF-{method.ToString().ToUpperInvariant()}-{Guid.CreateVersion7():N}";
        return Task.FromResult(Result.Success(new GatewayRefundResult(
            refundTxnId,
            amount,
            clock.GetUtcNow())));
    }
}
