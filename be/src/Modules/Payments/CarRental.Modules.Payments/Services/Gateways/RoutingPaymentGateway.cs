using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;
using CarRental.Modules.Payments.Errors;

namespace CarRental.Modules.Payments.Services.Gateways;

/// <summary>
/// Routes <see cref="IPaymentGateway"/> operations to the registered <see cref="IPaymentGatewayProvider"/>
/// for the requested <see cref="PaymentMethod"/>.
/// </summary>
internal sealed class RoutingPaymentGateway(IEnumerable<IPaymentGatewayProvider> providers) : IPaymentGateway
{
    private readonly Dictionary<PaymentMethod, IPaymentGatewayProvider> _providers =
        providers.ToDictionary(p => p.Method);

    public (string PaymentUrl, string QrCodeUrl, DateTimeOffset ExpiresAt) CreateCheckoutSession(
        Guid paymentId,
        Guid bookingId,
        decimal amount,
        PaymentMethod method,
        string returnUrl,
        DateTimeOffset now)
    {
        var provider = Resolve(method);
        return provider.CreateCheckoutSession(paymentId, bookingId, amount, returnUrl, now);
    }

    public bool VerifyWebhookSignature(PaymentWebhookRequest request)
    {
        return _providers.Values.Any(p => p.VerifyWebhookSignature(request));
    }

    public Task<Result<GatewayRefundResult>> RefundAsync(
        Guid paymentId,
        PaymentMethod method,
        string gatewayTransactionId,
        decimal amount,
        string reason,
        CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(method, out var provider))
            return Task.FromResult(Result.Failure<GatewayRefundResult>(PaymentsErrors.InvalidMethod));

        return provider.RefundAsync(paymentId, gatewayTransactionId, amount, reason, ct);
    }

    private IPaymentGatewayProvider Resolve(PaymentMethod method) =>
        _providers.TryGetValue(method, out var provider)
            ? provider
            : throw new NotSupportedException($"No payment gateway provider is registered for '{method}'.");
}
