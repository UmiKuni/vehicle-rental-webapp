using CarRental.Modules.Bookings.Contracts.Enums;
using CarRental.Modules.Bookings.Entities;

namespace CarRental.Modules.Bookings.Data.Repositories;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(BookingId id, CancellationToken ct = default);

    Task<Booking?> GetByIdReadOnlyAsync(BookingId id, CancellationToken ct = default);

    Task<(IReadOnlyList<Booking> Items, int TotalCount)> GetPagedByCustomerAsync(
        Guid customerId,
        BookingStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<(IReadOnlyList<Booking> Items, int TotalCount)> GetPagedByOwnerAsync(
        Guid ownerId,
        BookingStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<BookingId>> GetExpiredPendingIdsAsync(
        DateTimeOffset holdCutoff,
        CancellationToken ct = default);

    Task<IReadOnlyList<BookingId>> GetExpiredConfirmedIdsAsync(
        DateTimeOffset now,
        CancellationToken ct = default);

    void Add(Booking booking);
}
