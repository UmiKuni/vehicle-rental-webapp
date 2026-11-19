using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;
using CarRental.Modules.Payments.Errors;

namespace CarRental.Modules.Payments.Services.Gateways;

/// <summary>
/// Routes <see cref="IPaymentGateway"/> operations to the registered <see cref="IPaymentGatewayProvider"/>
/// for the requested <see cref="PaymentMethod"/>.
/// </summary>
public sealed class RoutingPaymentGateway(
    IEnumerable<IPaymentGatewayProvider> providers,
    IPayPalOrderCaptureProvider? payPalCaptureProvider = null) : IPaymentGateway
{
    private readonly Dictionary<PaymentMethod, IPaymentGatewayProvider> _providers =
        providers.ToDictionary(p => p.Method);

    public Task<Result<GatewayCheckoutResult>> CreateCheckoutSessionAsync(
        GatewayCheckoutRequest request,
        CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(request.Method, out var provider))
            return Task.FromResult(Result.Failure<GatewayCheckoutResult>(PaymentsErrors.InvalidMethod));

        return provider.CreateCheckoutSessionAsync(request, ct);
    }

    public bool VerifyWebhookSignature(PaymentWebhookRequest request) =>
        _providers.Values.Any(p => p.VerifyWebhookSignature(request));

    public Task<Result<GatewayWebhookNotification>> ParseAndVerifyWebhookAsync(
        GatewayRawWebhook webhook,
        CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(webhook.Method, out var provider))
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.InvalidMethod));

        return provider.ParseAndVerifyWebhookAsync(webhook, ct);
    }

    public Task<Result<GatewayWebhookNotification>> CapturePayPalOrderAsync(
        string paypalOrderId,
        CancellationToken ct = default) =>
        payPalCaptureProvider is null
            ? Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.InvalidMethod))
            : payPalCaptureProvider.CaptureOrderAsync(paypalOrderId, ct);

    public bool IsWebhookAmountValid(
        PaymentMethod method,
        decimal expectedPaymentAmount,
        string expectedCurrency,
        decimal webhookAmount,
        string webhookCurrency) =>
        _providers.TryGetValue(method, out var provider)
        && provider.IsWebhookAmountValid(expectedPaymentAmount, expectedCurrency, webhookAmount, webhookCurrency);

    public Task<Result<GatewayRefundResult>> RefundAsync(
        GatewayRefundRequest request,
        CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(request.Method, out var provider))
            return Task.FromResult(Result.Failure<GatewayRefundResult>(PaymentsErrors.InvalidMethod));

        return provider.RefundAsync(request, ct);
    }
}
