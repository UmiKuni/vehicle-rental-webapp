using CarRental.Modules.Bookings.Contracts.Enums;
using CarRental.Modules.Bookings.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Bookings.Data.Repositories;

internal sealed class BookingRepository(BookingDbContext db) : IBookingRepository
{
    public Task<Booking?> GetByIdAsync(BookingId id, CancellationToken ct = default) =>
        db.Bookings
            .AsSplitQuery()
            .SingleOrDefaultAsync(b => b.Id == id, ct);

    public Task<Booking?> GetByIdReadOnlyAsync(BookingId id, CancellationToken ct = default) =>
        db.Bookings
            .AsNoTracking()
            .AsSplitQuery()
            .SingleOrDefaultAsync(b => b.Id == id, ct);

    public async Task<(IReadOnlyList<Booking> Items, int TotalCount)> GetPagedByCustomerAsync(
        Guid customerId,
        BookingStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = db.Bookings
            .AsNoTracking()
            .Where(b => b.CustomerId == customerId);

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .AsSplitQuery()
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<Booking> Items, int TotalCount)> GetPagedByOwnerAsync(
        Guid ownerId,
        BookingStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = db.Bookings
            .AsNoTracking()
            .Where(b => b.OwnerId == ownerId);

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .AsSplitQuery()
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<BookingId>> GetExpiredPendingIdsAsync(
        DateTimeOffset holdCutoff,
        CancellationToken ct = default) =>
        await db.Bookings
            .AsNoTracking()
            .Where(b => b.Status == BookingStatus.Pending && b.CreatedAt <= holdCutoff)
            .Select(b => b.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<BookingId>> GetExpiredConfirmedIdsAsync(
        DateTimeOffset now,
        CancellationToken ct = default) =>
        await db.Bookings
            .AsNoTracking()
            .Where(b => b.Status == BookingStatus.Confirmed
                        && b.PaymentDeadline != null
                        && b.PaymentDeadline <= now)
            .Select(b => b.Id)
            .ToListAsync(ct);

    public void Add(Booking booking) =>
        db.Bookings.Add(booking);
}
