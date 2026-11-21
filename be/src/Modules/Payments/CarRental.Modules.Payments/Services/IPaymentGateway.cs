using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;

namespace CarRental.Modules.Payments.Services;

internal sealed record GatewayCheckoutRequest(
    Guid PaymentId,
    Guid BookingId,
    decimal Amount,
    string Currency,
    PaymentMethod Method,
    string ReturnUrl,
    DateTimeOffset Now,
    string? ClientIpAddress = null);

internal sealed record GatewayCheckoutResult(
    string PaymentUrl,
    string QrCodeUrl,
    DateTimeOffset ExpiresAt,
    string? GatewayReferenceId = null);

internal sealed record GatewayRefundRequest(
    Guid PaymentId,
    PaymentMethod Method,
    string GatewayTransactionId,
    decimal Amount,
    decimal OriginalAmount,
    string Currency,
    string Reason,
    DateTimeOffset? PaymentCompletedAt = null);

internal sealed record GatewayRefundResult(
    string RefundTransactionId,
    decimal RefundedAmount,
    DateTimeOffset ProcessedAt);

internal sealed record GatewayRawWebhook(
    PaymentMethod Method,
    string RawBody,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> QueryParameters);

internal sealed record GatewayWebhookNotification(
    PaymentMethod Method,
    string TransactionId,
    string OrderId,
    decimal Amount,
    string Currency,
    bool IsSuccess,
    string ResponseCode,
    string RawPayloadJson,
    bool IsIgnored = false,
    string? IgnoredReason = null,
    string? OrderIdToCapture = null);

internal interface IPaymentGateway
{
    Task<Result<GatewayCheckoutResult>> CreateCheckoutSessionAsync(
        GatewayCheckoutRequest request,
        CancellationToken ct = default);

    bool VerifyWebhookSignature(PaymentWebhookRequest request);

    Task<Result<GatewayWebhookNotification>> ParseAndVerifyWebhookAsync(
        GatewayRawWebhook webhook,
        CancellationToken ct = default);

    Task<Result<GatewayWebhookNotification>> CapturePayPalOrderAsync(
        string paypalOrderId,
        CancellationToken ct = default);

    bool IsWebhookAmountValid(
        PaymentMethod method,
        decimal expectedPaymentAmount,
        string expectedCurrency,
        decimal webhookAmount,
        string webhookCurrency);

    Task<Result<GatewayRefundResult>> RefundAsync(
        GatewayRefundRequest request,
        CancellationToken ct = default);
}
