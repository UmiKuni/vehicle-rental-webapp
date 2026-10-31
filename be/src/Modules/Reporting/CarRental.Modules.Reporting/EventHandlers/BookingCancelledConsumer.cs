using CarRental.Modules.Bookings.Contracts.Events;

namespace CarRental.Modules.Reporting.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="BookingCancelled"/>.
/// </summary>
public sealed class BookingCancelledConsumer
{
    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: BookingCancelled event from the Bookings module when a booking is cancelled.
     * 
     * Responsibilities:
     * 1. Check idempotency: Query ProcessedEvents table in ReportingDbContext by event.EventId.
     *    - If already processed, exit early.
     * 2. Compute the UTC+7 business date bucket from event.OccurredAt via DailyBookingStats.ToBucketDate().
     * 3. Query existing DailyBookingStats for the date bucket or initialize a new one via DailyBookingStats.Create(date).
     * 4. Call stats.ApplyBookingCancelled(event.OccurredAt) to increment CancellationCount.
     * 5. Record event.EventId in ProcessedEvents table.
     * 6. Persist changes to ReportingDbContext.
     */
    public Task Consume(BookingCancelled @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

