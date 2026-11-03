using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Reviews.Entities;

public class ReviewableBooking : Entity<Guid>
{
    public Guid BookingId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid CustomerId { get; private set; }
    public DateTimeOffset CompletedAt { get; private set; }
    public bool HasReviewed { get; private set; }

    private ReviewableBooking() { }

    public ReviewableBooking(
        Guid bookingId,
        Guid vehicleId,
        Guid customerId,
        DateTimeOffset completedAt)
    {
        Id = bookingId;
        BookingId = bookingId;
        VehicleId = vehicleId;
        CustomerId = customerId;
        CompletedAt = completedAt;
        HasReviewed = false;
    }

    public void MarkReviewed()
    {
        HasReviewed = true;
    }
}
