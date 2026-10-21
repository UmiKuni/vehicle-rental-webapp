using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Handover.Contracts.Events;

public sealed record HandoverConfirmed : IIntegrationEvent
{
    public Guid EventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid BookingId { get; }
    public string Type { get; }
    public int Odometer { get; }
    public int FuelLevel { get; }
    public DateTimeOffset ConfirmedAt { get; }
    public int? DistanceKm { get; }

    public HandoverConfirmed(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid bookingId,
        string type,
        int odometer,
        int fuelLevel,
        DateTimeOffset confirmedAt,
        int? distanceKm = null)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        BookingId = bookingId;
        Type = type;
        Odometer = odometer;
        FuelLevel = fuelLevel;
        ConfirmedAt = confirmedAt;
        DistanceKm = distanceKm;
    }
}
