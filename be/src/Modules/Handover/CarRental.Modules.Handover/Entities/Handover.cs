using CarRental.Modules.Handover.Errors;
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
            return Result.Failure<Handover>(HandoverErrors.InvalidContext);

        if (!ctx.IsActive)
            return Result.Failure<Handover>(HandoverErrors.InactiveContext);

        if (startedBy != ctx.CustomerId && startedBy != ctx.OwnerId)
            return Result.Failure<Handover>(HandoverErrors.Unauthorized5CEF8A);

        return Result.Success(new Handover(HandoverId.New(), ctx.BookingId, type, serverNow));
    }

    public Result AddPhoto(PhotoCorner corner, string storageKey, string fileHash, DateTimeOffset clientCapturedAt)
    {
        if (Status != HandoverStatus.Draft)
            return Result.Failure(HandoverErrors.ImmutableB8F729);

        if (string.IsNullOrWhiteSpace(storageKey))
            return Result.Failure(HandoverErrors.InvalidPhoto);

        if (string.IsNullOrWhiteSpace(fileHash))
            return Result.Failure(HandoverErrors.MissingHash);

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
            return Result.Failure(HandoverErrors.ImmutableA1098D);

        if (odo < 0)
            return Result.Failure(HandoverErrors.InvalidOdometer);

        if (fuelLevel < 0 || fuelLevel > 100)
            return Result.Failure(HandoverErrors.InvalidFuel);

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
            return Result.Failure(HandoverErrors.AlreadyConfirmed);

        if (actorId != customerId && actorId != ownerId)
            return Result.Failure(HandoverErrors.UnauthorizedE2911A);

        if (Condition == null)
            return Result.Failure(HandoverErrors.MissingCondition);

        if (Type == HandoverType.Return)
        {
            if (pickupOdometer.HasValue && Condition.Odometer < pickupOdometer.Value)
            {
                return Result.Failure(Error.Validation(
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
                return Result.Failure(Error.Validation("handover.missing_mandatory_photo", $"Missing mandatory photo for corner: {corner}."));
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
