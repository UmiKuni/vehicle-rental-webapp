using CarRental.Modules.Reviews.Contracts.Events;

namespace CarRental.Modules.Search.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="RatingSummaryUpdated"/>.
/// </summary>
public sealed class RatingSummaryUpdatedConsumer
{
    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: RatingSummaryUpdated event from the Reviews module when new reviews are approved or moderated.
     * 
     * Responsibilities:
     * 1. Query VehicleSearchDocument for event.VehicleId or initialize one if not yet created.
     * 2. Call document.ApplyRating(event.AverageRating, event.TotalReviews, event.OccurredAt).
     *    - Updates average rating badge and total reviews count on search cards.
     *    - Guarantees eventual consistency with LWW timestamp check (LastRatingEventAt).
     * 3. Persist changes to SearchDbContext.
     */
    public Task Consume(RatingSummaryUpdated @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

