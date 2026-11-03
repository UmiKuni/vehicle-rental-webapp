using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;

namespace CarRental.Modules.Search.Entities;

public class VehicleSearchDocument : AggregateRoot<Guid>
{
    public Guid VehicleId => Id;
    public GeoPoint Location { get; private set; } = default!;
    public string Brand { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public int Seats { get; private set; }
    public string Transmission { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public decimal? PriceFrom { get; private set; }
    public decimal? AverageRating { get; private set; }
    public int TotalReviews { get; private set; }
    public bool IsPublished { get; private set; }
    public DateTimeOffset? LastFleetEventAt { get; private set; }
    public DateTimeOffset? LastPricingEventAt { get; private set; }
    public DateTimeOffset? LastRatingEventAt { get; private set; }

    public bool IsVisible => IsPublished && PriceFrom.HasValue;

    private VehicleSearchDocument() { }

    public VehicleSearchDocument(Guid vehicleId)
    {
        Id = vehicleId;
        IsPublished = false;
        TotalReviews = 0;
    }

    public static VehicleSearchDocument Create(Guid vehicleId)
    {
        return new VehicleSearchDocument(vehicleId);
    }

    public bool ApplyPublished(
        string brand,
        string model,
        int seats,
        string transmission,
        GeoPoint location,
        string address,
        DateTimeOffset occurredAt)
    {
        if (LastFleetEventAt.HasValue && occurredAt < LastFleetEventAt.Value)
            return false;

        Brand = brand;
        Model = model;
        Seats = seats;
        Transmission = transmission;
        Location = location;
        Address = address;
        IsPublished = true;
        LastFleetEventAt = occurredAt;

        return true;
    }

    public bool ApplyUnpublished(DateTimeOffset occurredAt)
    {
        if (LastFleetEventAt.HasValue && occurredAt < LastFleetEventAt.Value)
            return false;

        IsPublished = false;
        LastFleetEventAt = occurredAt;

        return true;
    }

    public bool ApplyPrice(decimal priceFrom, DateTimeOffset occurredAt)
    {
        if (LastPricingEventAt.HasValue && occurredAt < LastPricingEventAt.Value)
            return false;

        PriceFrom = priceFrom;
        LastPricingEventAt = occurredAt;

        return true;
    }

    public bool ApplyRating(decimal averageRating, int totalReviews, DateTimeOffset occurredAt)
    {
        if (LastRatingEventAt.HasValue && occurredAt < LastRatingEventAt.Value)
            return false;

        AverageRating = averageRating;
        TotalReviews = totalReviews;
        LastRatingEventAt = occurredAt;

        return true;
    }
}
