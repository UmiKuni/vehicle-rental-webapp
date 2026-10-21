using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Reviews.Contracts.Events;

public sealed record RatingSummaryUpdated : IIntegrationEvent
{
    public Guid EventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid VehicleId { get; }
    public decimal AverageRating { get; }
    public int TotalReviews { get; }

    public RatingSummaryUpdated(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid vehicleId,
        decimal averageRating,
        int totalReviews)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        VehicleId = vehicleId;
        AverageRating = averageRating;
        TotalReviews = totalReviews;
    }
}
