using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;

namespace CarRental.Modules.Payments.Services;

/// <summary>
/// Gateway-specific provider contract for a single <see cref="PaymentMethod"/>.
/// </summary>
internal interface IPaymentGatewayProvider
{
    PaymentMethod Method { get; }

    Task<Result<GatewayCheckoutResult>> CreateCheckoutSessionAsync(
        GatewayCheckoutRequest request,
        CancellationToken ct = default);

    bool VerifyWebhookSignature(PaymentWebhookRequest request);

    Task<Result<GatewayWebhookNotification>> ParseAndVerifyWebhookAsync(
        GatewayRawWebhook webhook,
        CancellationToken ct = default);

    bool IsWebhookAmountValid(
        decimal expectedPaymentAmount,
        string expectedCurrency,
        decimal webhookAmount,
        string webhookCurrency);

    Task<Result<GatewayRefundResult>> RefundAsync(
        GatewayRefundRequest request,
        CancellationToken ct = default);
}

internal interface IPayPalOrderCaptureProvider
{
    Task<Result<GatewayWebhookNotification>> CaptureOrderAsync(
        string paypalOrderId,
        CancellationToken ct = default);
}
