using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Reporting.Entities;

internal static class BookingProjectionStatus
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Paid = "Paid";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string Rejected = "Rejected";
    public const string Expired = "Expired";
}

/// <summary>
/// Read-model projection of a booking in the reporting schema so dashboard and owner revenue
/// queries execute locally without cross-schema joins.
/// </summary>
internal sealed class BookingReportProjection : Entity<Guid>
{
    public Guid OwnerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public string VehiclePlate { get; private set; } = string.Empty;
    public string VehicleModel { get; private set; } = string.Empty;
    public DateTimeOffset StartTime { get; private set; }
    public DateTimeOffset EndTime { get; private set; }
    public decimal RentAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    private BookingReportProjection()
    {
    }

    private BookingReportProjection(
        Guid bookingId,
        Guid ownerId,
        Guid vehicleId,
        string vehiclePlate,
        string vehicleModel,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        decimal rentAmount,
        DateTimeOffset createdAt) : base(bookingId)
    {
        OwnerId = ownerId;
        VehicleId = vehicleId;
        VehiclePlate = vehiclePlate;
        VehicleModel = vehicleModel;
        StartTime = startTime;
        EndTime = endTime;
        RentAmount = rentAmount;
        PaidAmount = 0m;
        Status = BookingProjectionStatus.Pending;
        CreatedAt = createdAt;
    }

    public static BookingReportProjection Create(
        Guid bookingId,
        Guid ownerId,
        Guid vehicleId,
        string vehiclePlate,
        string vehicleModel,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        decimal rentAmount,
        DateTimeOffset createdAt)
    {
        return new BookingReportProjection(
            bookingId,
            ownerId,
            vehicleId,
            vehiclePlate,
            vehicleModel,
            startTime,
            endTime,
            rentAmount,
            createdAt);
    }

    public void SetVehicleInfo(string plate, string model)
    {
        VehiclePlate = plate;
        VehicleModel = model;
    }

    public void MarkConfirmed()
    {
        if (Status == BookingProjectionStatus.Pending)
            Status = BookingProjectionStatus.Confirmed;
    }

    public void MarkPaid(DateTimeOffset paidAt)
    {
        PaidAt ??= paidAt;

        if (Status is BookingProjectionStatus.Pending or BookingProjectionStatus.Confirmed)
            Status = BookingProjectionStatus.Paid;
    }

    public void RecordPayment(decimal amount, DateTimeOffset paidAt)
    {
        if (amount > 0m)
            PaidAmount += amount;

        PaidAt ??= paidAt;

        if (Status is BookingProjectionStatus.Pending or BookingProjectionStatus.Confirmed)
            Status = BookingProjectionStatus.Paid;
    }

    public void MarkInProgress()
    {
        if (Status is BookingProjectionStatus.Pending or BookingProjectionStatus.Confirmed or BookingProjectionStatus.Paid)
            Status = BookingProjectionStatus.InProgress;
    }

    public void MarkCompleted(DateTimeOffset completedAt)
    {
        CompletedAt ??= completedAt;
        Status = BookingProjectionStatus.Completed;
    }

    public void MarkCancelled(DateTimeOffset cancelledAt)
    {
        CancelledAt ??= cancelledAt;
        Status = BookingProjectionStatus.Cancelled;
    }

    public void MarkRejected()
    {
        Status = BookingProjectionStatus.Rejected;
    }

    public void MarkExpired()
    {
        Status = BookingProjectionStatus.Expired;
    }
}
