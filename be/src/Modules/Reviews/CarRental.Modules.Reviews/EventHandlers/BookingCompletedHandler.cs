using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Reviews.Data;
using CarRental.Modules.Reviews.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Reviews.EventHandlers;

internal sealed class BookingCompletedHandler
{
    public async Task Handle(
        BookingCompleted @event,
        ReviewsDbContext dbContext,
        CancellationToken ct)
    {
        var alreadyExists = await dbContext.ReviewableBookings
            .AnyAsync(item => item.BookingId == @event.BookingId, ct);
        if (alreadyExists)
            return;

        dbContext.ReviewableBookings.Add(new ReviewableBooking(
            @event.BookingId,
            @event.VehicleId,
            @event.CustomerId,
            @event.OccurredAt));
        await dbContext.SaveChangesAsync(ct);
    }
}
