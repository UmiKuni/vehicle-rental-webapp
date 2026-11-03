using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Bookings.Contracts.Dtos;

namespace CarRental.Modules.Bookings.Contracts;

/// <summary>
/// Synchronous in-process contract for booking verification and lifecycle transitions.
/// </summary>
public interface IBookingApi
{
    Task<bool> IsParticipantAsync(Guid bookingId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Synchronously marks a booking as Paid upon successful payment checkout/webhook verification.
    /// </summary>
    Task<Result> MarkPaidAsync(Guid bookingId, Guid paymentId, DateTimeOffset paidAt, CancellationToken ct = default);

    /// <summary>
    /// Retrieves live rental details for Handover and dispute verification.
    /// </summary>
    Task<Result<BookingRentalDetailsDto>> GetRentalDetailsAsync(Guid bookingId, CancellationToken ct = default);

    /// <summary>
    /// Synchronously starts the rental trip (Paid -> InProgress) when pickup handover is confirmed.
    /// </summary>
    Task<Result> StartTripAsync(Guid bookingId, DateTimeOffset startedAt, CancellationToken ct = default);

    /// <summary>
    /// Synchronously completes the rental trip (InProgress -> Completed) when return handover is confirmed.
    /// </summary>
    Task<Result> CompleteTripAsync(Guid bookingId, decimal? overKmFee, DateTimeOffset completedAt, CancellationToken ct = default);
}
