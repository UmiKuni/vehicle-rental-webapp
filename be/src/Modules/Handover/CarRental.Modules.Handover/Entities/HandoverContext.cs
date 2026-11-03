using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Handover.Entities;

public class HandoverContext : Entity<Guid>
{
    public Guid BookingId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateTimeOffset StartTime { get; private set; }
    public DateTimeOffset EndTime { get; private set; }
    public bool IsActive { get; private set; }

    private HandoverContext() { }

    public HandoverContext(
        Guid bookingId,
        Guid vehicleId,
        Guid customerId,
        Guid ownerId,
        DateTimeOffset startTime,
        DateTimeOffset endTime)
    {
        Id = bookingId;
        BookingId = bookingId;
        VehicleId = vehicleId;
        CustomerId = customerId;
        OwnerId = ownerId;
        StartTime = startTime;
        EndTime = endTime;
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
