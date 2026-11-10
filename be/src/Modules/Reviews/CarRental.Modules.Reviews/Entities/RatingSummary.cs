using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Reviews.Entities;

public class RatingSummary : AggregateRoot<Guid>
{
    public Guid VehicleId => Id;
    public decimal AverageRating { get; private set; }
    public int TotalReviews { get; private set; }

    private RatingSummary() { }

    private RatingSummary(Guid vehicleId)
    {
        Id = vehicleId;
        AverageRating = 0m;
        TotalReviews = 0;
    }

    public static RatingSummary Create(Guid vehicleId)
    {
        return new RatingSummary(vehicleId);
    }

    public void AddRating(int rating)
    {
        var clamped = Math.Clamp(rating, 1, 5);
        var newTotal = TotalReviews + 1;
        var newAverage = ((AverageRating * TotalReviews) + clamped) / newTotal;

        TotalReviews = newTotal;
        AverageRating = Math.Round(newAverage, 2, MidpointRounding.AwayFromZero);
    }

    public void RemoveRating(int rating)
    {
        if (TotalReviews <= 1)
        {
            TotalReviews = 0;
            AverageRating = 0m;
            return;
        }

        var clamped = Math.Clamp(rating, 1, 5);
        var newTotal = TotalReviews - 1;
        var newAverage = ((AverageRating * TotalReviews) - clamped) / newTotal;

        TotalReviews = newTotal;
        AverageRating = Math.Round(Math.Max(0m, newAverage), 2, MidpointRounding.AwayFromZero);
    }
}
