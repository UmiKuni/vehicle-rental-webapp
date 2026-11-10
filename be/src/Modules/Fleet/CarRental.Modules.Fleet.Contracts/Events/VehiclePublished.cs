using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Fleet.Contracts.Events;

public sealed record VehiclePublished : IIntegrationEvent
{
    public Guid EventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid VehicleId { get; }
    public Guid OwnerId { get; }
    public string Brand { get; }
    public string Model { get; }
    public int Seats { get; }
    public string Transmission { get; }
    public GeoPoint Location { get; }
    public string Address { get; }

    public VehiclePublished(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid vehicleId,
        Guid ownerId,
        string brand,
        string model,
        int seats,
        string transmission,
        GeoPoint location,
        string address)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        VehicleId = vehicleId;
        OwnerId = ownerId;
        Brand = brand;
        Model = model;
        Seats = seats;
        Transmission = transmission;
        Location = location;
        Address = address;
    }
}
