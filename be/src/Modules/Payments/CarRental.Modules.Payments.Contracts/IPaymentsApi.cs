using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;

namespace CarRental.Modules.Payments.Contracts;

/// <summary>
/// Synchronous in-process contract for payment and deposit operations.
/// </summary>
public interface IPaymentsApi
{
    /// <summary>
    /// Creates or retrieves a checkout session for an approved booking on demand.
    /// </summary>
    Task<Result<PaymentCheckoutDto>> CreateCheckoutAsync(
        Guid bookingId,
        decimal amount,
        decimal depositPortion,
        PaymentMethod method,
        string returnUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Initiates checkout for an authenticated customer after verifying booking ownership and payable amount.
    /// </summary>
    Task<Result<PaymentCheckoutDto>> InitiateCheckoutAsync(
        Guid customerId,
        InitiatePaymentRequest request,
        CancellationToken ct = default);

    Task<Result<PaymentDetailDto>> GetPaymentByIdAsync(
        Guid paymentId,
        Guid actorId,
        bool isAdmin = false,
        CancellationToken ct = default);

    Task<Result<BookingPaymentSummaryDto>> GetPaymentByBookingAsync(
        Guid bookingId,
        Guid actorId,
        bool isAdmin = false,
        CancellationToken ct = default);

    Task<Result<PaymentWebhookResponseDto>> HandleWebhookAsync(
        PaymentWebhookRequest request,
        CancellationToken ct = default);

    Task<Result<PaymentWebhookResponseDto>> HandleGatewayWebhookAsync(
        GatewayWebhookRequest request,
        CancellationToken ct = default);

    Task<Result<PaymentDetailDto>> CapturePayPalOrderAsync(
        string paypalOrderId,
        Guid actorId,
        bool isAdmin = false,
        CancellationToken ct = default);

    Task<Result<PaymentRefundDto>> RefundPaymentAsync(
        Guid paymentId,
        Guid actorId,
        bool isAdmin,
        decimal amount,
        string reason,
        CancellationToken ct = default);

    Task<Result<DepositActionDto>> ReleaseDepositByIdAsync(
        Guid depositId,
        Guid actorId,
        bool isAdmin = false,
        string? note = null,
        CancellationToken ct = default);

    Task<Result<DepositActionDto>> DeductDepositByIdAsync(
        Guid depositId,
        Guid actorId,
        bool isAdmin = false,
        decimal deductAmount = 0m,
        string reason = "",
        CancellationToken ct = default);

    /// <summary>
    /// Freezes an active security deposit when a support dispute ticket is opened.
    /// </summary>
    Task<Result> FreezeDepositAsync(
        Guid bookingId,
        CancellationToken ct = default);

    /// <summary>
    /// Settles the security deposit based on an administrative dispute ruling.
    /// </summary>
    Task<Result> SettleDepositAsync(
        Guid bookingId,
        int depositToCustomerPercent,
        CancellationToken ct = default);

    /// <summary>
    /// Releases deposit upon normal trip completion after inspection period.
    /// </summary>
    Task<Result> ReleaseDepositAsync(
        Guid bookingId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves current deposit status for a booking.
    /// </summary>
    Task<Result<DepositDetailsDto>> GetDepositAsync(
        Guid bookingId,
        CancellationToken ct = default);
}
