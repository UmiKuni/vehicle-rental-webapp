using CarRental.Modules.Bookings.Contracts.Events;

namespace CarRental.Modules.Reporting.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="BookingRequested"/>.
/// </summary>
public sealed class BookingRequestedConsumer
{
    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: BookingRequested event from the Bookings module when a customer places a reservation.
     * 
     * Responsibilities:
     * 1. Check idempotency: Query ProcessedEvents table in ReportingDbContext by event.EventId.
     *    - If already processed, exit early (at-most-once delivery).
     * 2. Compute the UTC+7 business date bucket from event.OccurredAt via DailyBookingStats.ToBucketDate().
     * 3. Query existing DailyBookingStats for the date bucket or initialize a new one via DailyBookingStats.Create(date).
     * 4. Call stats.ApplyBookingRequested(event.OccurredAt) to increment TotalBookings.
     * 5. Record event.EventId in ProcessedEvents table.
     * 6. Persist changes to ReportingDbContext.
     */
    public Task Consume(BookingRequested @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

