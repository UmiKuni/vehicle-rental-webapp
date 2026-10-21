using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Handover.Contracts.Enums;

namespace CarRental.Modules.Handover.Entities;

public class Handover : AggregateRoot<HandoverId>
{
    private readonly List<HandoverPhoto> _photos = new();

    public Guid BookingId { get; private set; }
    public HandoverType Type { get; private set; }
    public DateTimeOffset ServerReceivedAt { get; private set; }
    public HandoverStatus Status { get; private set; }
    public DateTimeOffset? ConfirmedByCustomerAt { get; private set; }
    public DateTimeOffset? ConfirmedByOwnerAt { get; private set; }
    public VehicleCondition? Condition { get; private set; }

    public IReadOnlyCollection<HandoverPhoto> Photos => _photos.AsReadOnly();

    private Handover() { }

    private Handover(
        HandoverId id,
        Guid bookingId,
        HandoverType type,
        DateTimeOffset serverNow)
    {
        Id = id;
        BookingId = bookingId;
        Type = type;
        ServerReceivedAt = serverNow;
        Status = HandoverStatus.Draft;
    }

    public static Result<Handover> Start(
        HandoverContext ctx,
        HandoverType type,
        Guid startedBy,
        DateTimeOffset serverNow)
    {
        if (ctx == null)
            return Result.Failure<Handover>(new Error("handover.invalid_context", "Handover context is required."));

        if (!ctx.IsActive)
            return Result.Failure<Handover>(new Error("handover.inactive_context", "Booking is not active for handover."));

        if (startedBy != ctx.CustomerId && startedBy != ctx.OwnerId)
            return Result.Failure<Handover>(new Error("handover.unauthorized", "Only the customer or owner can initiate handover."));

        return Result.Success(new Handover(HandoverId.New(), ctx.BookingId, type, serverNow));
    }

    public Result AddPhoto(PhotoCorner corner, string storageKey, string fileHash, DateTimeOffset clientCapturedAt)
    {
        if (Status != HandoverStatus.Draft)
            return Result.Failure(new Error("handover.immutable", "Photos can only be added to draft handovers."));

        if (string.IsNullOrWhiteSpace(storageKey))
            return Result.Failure(new Error("handover.invalid_photo", "Photo storage key cannot be empty."));

        if (string.IsNullOrWhiteSpace(fileHash))
            return Result.Failure(new Error("handover.missing_hash", "Tamper-verification file hash is mandatory."));

        var existing = _photos.FirstOrDefault(p => p.Corner == corner);
        if (existing != null)
        {
            _photos.Remove(existing);
        }

        _photos.Add(new HandoverPhoto(
            Guid.CreateVersion7(),
            Id,
            corner,
            storageKey.Trim(),
            fileHash.Trim(),
            clientCapturedAt));

        return Result.Success();
    }

    public Result SetCondition(int odo, int fuelLevel, string? damageNote)
    {
        if (Status != HandoverStatus.Draft)
            return Result.Failure(new Error("handover.immutable", "Vehicle condition can only be updated for draft handovers."));

        if (odo < 0)
            return Result.Failure(new Error("handover.invalid_odometer", "Odometer reading cannot be negative."));

        if (fuelLevel < 0 || fuelLevel > 100)
            return Result.Failure(new Error("handover.invalid_fuel", "Fuel level must be between 0 and 100 percent."));

        Condition = new VehicleCondition(Id, odo, fuelLevel, damageNote?.Trim());
        return Result.Success();
    }

    public Result Confirm(
        Guid actorId,
        Guid customerId,
        Guid ownerId,
        DateTimeOffset now,
        int? pickupOdometer = null)
    {
        if (Status != HandoverStatus.Draft)
            return Result.Failure(new Error("handover.already_confirmed", "Handover has already been confirmed and is immutable."));

        if (actorId != customerId && actorId != ownerId)
            return Result.Failure(new Error("handover.unauthorized", "Only the customer or owner can confirm this handover."));

        if (Condition == null)
            return Result.Failure(new Error("handover.missing_condition", "Vehicle condition must be recorded before confirmation."));

        if (Type == HandoverType.Return)
        {
            if (pickupOdometer.HasValue && Condition.Odometer < pickupOdometer.Value)
            {
                return Result.Failure(new Error(
                    "handover.odometer_decreased",
                    $"Return odometer ({Condition.Odometer}) cannot be less than pickup odometer ({pickupOdometer.Value})."));
            }
        }

        var mandatoryCorners = new[]
        {
            PhotoCorner.FrontLeft,
            PhotoCorner.FrontRight,
            PhotoCorner.RearLeft,
            PhotoCorner.RearRight,
            PhotoCorner.Dash
        };

        foreach (var corner in mandatoryCorners)
        {
            if (!_photos.Any(p => p.Corner == corner))
            {
                return Result.Failure(new Error("handover.missing_mandatory_photo", $"Missing mandatory photo for corner: {corner}."));
            }
        }

        if (actorId == customerId)
        {
            ConfirmedByCustomerAt = now;
        }

        if (actorId == ownerId)
        {
            ConfirmedByOwnerAt = now;
        }

        if (ConfirmedByCustomerAt.HasValue && ConfirmedByOwnerAt.HasValue)
        {
            Status = HandoverStatus.Confirmed;
        }

        return Result.Success();
    }
}
