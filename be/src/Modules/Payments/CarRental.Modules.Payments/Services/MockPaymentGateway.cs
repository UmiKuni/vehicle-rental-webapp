using System.Globalization;
using System.Text.Json;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;
using CarRental.Modules.Payments.Data.Repositories;
using CarRental.Modules.Payments.Entities;
using CarRental.Modules.Payments.Errors;
using CarRental.Modules.Payments.Services.Gateways;
using Microsoft.Extensions.Configuration;

namespace CarRental.Modules.Payments.Services;

internal sealed class MockPaymentGateway(
    IConfiguration configuration,
    TimeProvider clock,
    IPaymentsRepository? repository = null) : IPaymentGateway
{
    private static readonly TimeSpan CheckoutTtl = TimeSpan.FromMinutes(15);

    public Task<Result<GatewayCheckoutResult>> CreateCheckoutSessionAsync(
        GatewayCheckoutRequest request,
        CancellationToken ct = default)
    {
        var amountVnd = (long)Math.Round(request.Amount, MidpointRounding.AwayFromZero);
        var encodedReturnUrl = Uri.EscapeDataString(request.ReturnUrl);
        var expiresAt = request.Now.Add(CheckoutTtl);

        var paymentUrl = request.Method switch
        {
            PaymentMethod.VNPay => FormattableString.Invariant(
                $"https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_TxnRef={request.PaymentId:N}&vnp_Amount={amountVnd * 100}&vnp_ReturnUrl={encodedReturnUrl}"),
            PaymentMethod.Paypal => FormattableString.Invariant(
                $"https://www.sandbox.paypal.com/checkoutnow?token={request.PaymentId:N}&amount={amountVnd}&returnUrl={encodedReturnUrl}"),
            PaymentMethod.Stripe => FormattableString.Invariant(
                $"https://checkout.stripe.com/c/pay/{request.PaymentId:N}?amount={amountVnd}&redirect_url={encodedReturnUrl}"),
            _ => FormattableString.Invariant(
                $"https://sandbox.vietqr.io/pay?orderId={request.PaymentId:N}&bookingId={request.BookingId:N}&amount={amountVnd}&returnUrl={encodedReturnUrl}")
        };

        var qrPayload = FormattableString.Invariant(
            $"00020101021238580010A000000727|ref={request.PaymentId:N}|amount={amountVnd}");
        var qrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=250x250&data={Uri.EscapeDataString(qrPayload)}";

        return Task.FromResult(Result.Success(new GatewayCheckoutResult(
            paymentUrl,
            qrCodeUrl,
            expiresAt,
            request.PaymentId.ToString("N"))));
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

    public Task<Result<GatewayWebhookNotification>> ParseAndVerifyWebhookAsync(
        GatewayRawWebhook webhook,
        CancellationToken ct = default)
    {
        if (webhook.Method == PaymentMethod.VNPay)
        {
            if (!GatewayCrypto.TryGetCaseInsensitiveValue(webhook.QueryParameters, "vnp_TxnRef", out var orderId))
                return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.WebhookInvalidOrder));

            GatewayCrypto.TryGetCaseInsensitiveValue(webhook.QueryParameters, "vnp_Amount", out var rawAmountText);
            long.TryParse(rawAmountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawAmount);
            GatewayCrypto.TryGetCaseInsensitiveValue(webhook.QueryParameters, "vnp_ResponseCode", out var responseCode);
            GatewayCrypto.TryGetCaseInsensitiveValue(webhook.QueryParameters, "vnp_TransactionNo", out var txnNo);
            GatewayCrypto.TryGetCaseInsensitiveValue(webhook.QueryParameters, "vnp_SecureHash", out var sig);

            var amount = rawAmount / 100m;
            var txnId = string.IsNullOrWhiteSpace(txnNo) ? $"MOCK-VNP-{Guid.CreateVersion7():N}" : txnNo;
            var code = string.IsNullOrWhiteSpace(responseCode) ? "00" : responseCode;

            if (!VerifyWebhookSignature(new PaymentWebhookRequest(txnId, orderId, amount, code, sig)))
                return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.InvalidSignature));

            return Task.FromResult(Result.Success(new GatewayWebhookNotification(
                PaymentMethod.VNPay,
                txnId,
                orderId,
                amount,
                "VND",
                string.Equals(code, "00", StringComparison.Ordinal),
                code,
                JsonSerializer.Serialize(webhook.QueryParameters))));
        }

        try
        {
            var req = JsonSerializer.Deserialize<PaymentWebhookRequest>(
                webhook.RawBody,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (req is null)
                return Task.FromResult(Result.Failure<GatewayWebhookNotification>(
                    PaymentsErrors.WebhookPayloadInvalid("Empty webhook payload.")));

            if (!VerifyWebhookSignature(req))
                return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.InvalidSignature));

            return Task.FromResult(Result.Success(new GatewayWebhookNotification(
                webhook.Method,
                req.TransactionId,
                req.OrderId,
                req.Amount,
                "VND",
                string.Equals(req.ResponseCode?.Trim(), "00", StringComparison.Ordinal),
                req.ResponseCode?.Trim() ?? string.Empty,
                webhook.RawBody)));
        }
        catch (JsonException)
        {
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(
                PaymentsErrors.WebhookPayloadInvalid("Invalid mock webhook JSON payload.")));
        }
    }

    public async Task<Result<GatewayWebhookNotification>> CapturePayPalOrderAsync(
        string paypalOrderId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(paypalOrderId))
            return Result.Failure<GatewayWebhookNotification>(PaymentsErrors.InvalidPayPalOrderId);

        var trimmedOrderId = paypalOrderId.Trim();
        var amount = 0m;
        var currency = "VND";

        if (repository is not null && Guid.TryParse(trimmedOrderId, out var orderGuid) && orderGuid != Guid.Empty)
        {
            var payment = await repository.GetPaymentByIdAsync(PaymentId.From(orderGuid), ct)
                          ?? await repository.GetPendingPaymentByBookingIdAsync(orderGuid, ct);
            if (payment is not null)
            {
                amount = payment.Amount.Amount;
                currency = payment.Amount.Currency;
            }
        }

        return Result.Success(new GatewayWebhookNotification(
            PaymentMethod.Paypal,
            $"MOCK-PP-CAP-{Guid.CreateVersion7():N}",
            trimmedOrderId,
            amount,
            currency,
            true,
            "00",
            JsonSerializer.Serialize(new { orderId = trimmedOrderId, amount, currency, mock = true })));
    }

    public bool IsWebhookAmountValid(
        PaymentMethod method,
        decimal expectedPaymentAmount,
        string expectedCurrency,
        decimal webhookAmount,
        string webhookCurrency) =>
        webhookAmount == expectedPaymentAmount
        && string.Equals(webhookCurrency, expectedCurrency, StringComparison.OrdinalIgnoreCase);

    public Task<Result<GatewayRefundResult>> RefundAsync(
        GatewayRefundRequest request,
        CancellationToken ct = default)
    {
        var refundTxnId = $"REF-{request.Method.ToString().ToUpperInvariant()}-{Guid.CreateVersion7():N}";
        return Task.FromResult(Result.Success(new GatewayRefundResult(
            refundTxnId,
            request.Amount,
            clock.GetUtcNow())));
    }
}
