namespace CarRental.Modules.Bookings.Contracts;

public interface IBookingApi
{
    Task<bool> IsParticipantAsync(Guid bookingId, Guid userId, CancellationToken ct = default);
}
