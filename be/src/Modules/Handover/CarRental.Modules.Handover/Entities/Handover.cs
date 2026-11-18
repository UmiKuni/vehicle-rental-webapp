using CarRental.Modules.Handover.Errors;
using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Handover.Contracts.Enums;

namespace CarRental.Modules.Handover.Entities;

public class Handover : AggregateRoot<HandoverId>
{
    private static readonly PhotoCorner[] MandatoryCorners =
    [
        PhotoCorner.FrontLeft,
        PhotoCorner.FrontRight,
        PhotoCorner.RearLeft,
        PhotoCorner.RearRight,
        PhotoCorner.Dash
    ];

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
            return HandoverErrors.InvalidContext;

        if (!ctx.IsActive)
            return HandoverErrors.InactiveContext;

        if (startedBy != ctx.CustomerId && startedBy != ctx.OwnerId)
            return HandoverErrors.NotParticipant;

        return new Handover(HandoverId.New(), ctx.BookingId, type, serverNow);
    }

    public Result AddPhoto(PhotoCorner corner, string storageKey, string fileHash, DateTimeOffset clientCapturedAt)
    {
        if (Status != HandoverStatus.Draft)
            return HandoverErrors.Immutable;

        if (string.IsNullOrWhiteSpace(storageKey))
            return HandoverErrors.InvalidPhoto;

        if (string.IsNullOrWhiteSpace(fileHash))
            return HandoverErrors.MissingHash;

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
            return HandoverErrors.Immutable;

        if (odo < 0)
            return HandoverErrors.InvalidOdometer;

        if (fuelLevel < 0 || fuelLevel > 100)
            return HandoverErrors.InvalidFuel;

        if (Condition is null)
        {
            Condition = new VehicleCondition(Id, odo, fuelLevel, damageNote?.Trim());
        }
        else
        {
            Condition.Update(odo, fuelLevel, damageNote?.Trim());
        }

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
            return HandoverErrors.AlreadyConfirmed;

        if (actorId != customerId && actorId != ownerId)
            return HandoverErrors.NotParticipant;

        if (Condition == null)
            return HandoverErrors.MissingCondition;

        if (Type == HandoverType.Return && pickupOdometer.HasValue && Condition.Odometer < pickupOdometer.Value)
            return HandoverErrors.OdometerDecreased(Condition.Odometer, pickupOdometer.Value);

        foreach (var corner in MandatoryCorners)
        {
            if (!_photos.Any(p => p.Corner == corner))
                return HandoverErrors.MissingMandatoryPhoto(corner);
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
