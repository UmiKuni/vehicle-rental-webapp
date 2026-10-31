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

