using CarRental.Modules.Fleet.Errors;
using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Fleet.Contracts.Enums;

namespace CarRental.Modules.Fleet.Entities;

public class Vehicle : AggregateRoot<VehicleId>
{
    private readonly List<VehicleImage> _images = new();
    private readonly List<VehicleApproval> _approvals = new();

    public Guid OwnerId { get; private set; }
    public string PlateNumber { get; private set; } = string.Empty;
    public Guid BrandId { get; private set; }
    public Guid ModelId { get; private set; }
    public TransmissionType Transmission { get; private set; }
    public int Seats { get; private set; }
    public VehicleStatus Status { get; private set; }
    public GeoPoint Location { get; private set; } = default!;
    public string Address { get; private set; } = string.Empty;
    public bool HiddenByAdmin { get; private set; }

    public IReadOnlyCollection<VehicleImage> Images => _images.AsReadOnly();
    public IReadOnlyCollection<VehicleApproval> Approvals => _approvals.AsReadOnly();

    private Vehicle() { }

    private Vehicle(
        VehicleId id,
        Guid ownerId,
        string plateNumber,
        Guid brandId,
        Guid modelId,
        TransmissionType transmission,
        int seats,
        GeoPoint location,
        string address)
    {
        Id = id;
        OwnerId = ownerId;
        PlateNumber = plateNumber.Trim().ToUpperInvariant();
        BrandId = brandId;
        ModelId = modelId;
        Transmission = transmission;
        Seats = seats;
        Location = location;
        Address = address.Trim();
        Status = VehicleStatus.Draft;
        HiddenByAdmin = false;
    }

    public static Result<Vehicle> Create(
        Guid ownerId,
        string plateNumber,
        Guid brandId,
        Guid modelId,
        TransmissionType transmission,
        int seats,
        GeoPoint location,
        string address)
    {
        if (ownerId == Guid.Empty)
            return Result.Failure<Vehicle>(FleetErrors.InvalidOwner);

        if (string.IsNullOrWhiteSpace(plateNumber))
            return Result.Failure<Vehicle>(FleetErrors.InvalidPlate);

        if (seats <= 0)
            return Result.Failure<Vehicle>(FleetErrors.InvalidSeats);

        if (location == null)
            return Result.Failure<Vehicle>(FleetErrors.InvalidLocation);

        if (string.IsNullOrWhiteSpace(address))
            return Result.Failure<Vehicle>(FleetErrors.InvalidAddress);

        return Result.Success(new Vehicle(VehicleId.New(), ownerId, plateNumber, brandId, modelId, transmission, seats, location, address));
    }

    public Result UpdateDetails(
        Guid brandId,
        Guid modelId,
        TransmissionType transmission,
        int seats,
        GeoPoint location,
        string address)
    {
        if (seats <= 0)
            return Result.Failure(FleetErrors.InvalidSeats);

        if (location == null)
            return Result.Failure(FleetErrors.InvalidLocation);

        BrandId = brandId;
        ModelId = modelId;
        Transmission = transmission;
        Seats = seats;
        Location = location;
        Address = address.Trim();

        return Result.Success();
    }

    public Result AddImage(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            return Result.Failure(FleetErrors.InvalidImage);

        bool isPrimary = _images.Count == 0;
        _images.Add(new VehicleImage(Guid.CreateVersion7(), Id, storageKey.Trim(), isPrimary));

        return Result.Success();
    }

    public Result RemoveImage(Guid imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId);
        if (image == null)
            return Result.Failure(FleetErrors.ImageNotFound);

        bool wasPrimary = image.IsPrimary;
        _images.Remove(image);

        if (wasPrimary && _images.Count > 0)
        {
            _images[0].SetPrimary(true);
        }

        return Result.Success();
    }

    public Result SetPrimaryImage(Guid imageId)
    {
        var target = _images.FirstOrDefault(i => i.Id == imageId);
        if (target == null)
            return Result.Failure(FleetErrors.ImageNotFound);

        foreach (var img in _images)
        {
            img.SetPrimary(img.Id == imageId);
        }

        return Result.Success();
    }

    public Result SubmitForApproval()
    {
        if (Status != VehicleStatus.Draft)
            return Result.Failure(FleetErrors.InvalidStateFCC971);

        if (!_images.Any(i => i.IsPrimary))
            return Result.Failure(FleetErrors.MissingPrimaryImage);

        if (_images.Count < 3)
            return Result.Failure(FleetErrors.InsufficientImages);

        Status = VehicleStatus.PendingApproval;
        return Result.Success();
    }

    public Result Approve(Guid adminId)
    {
        if (Status != VehicleStatus.PendingApproval)
            return Result.Failure(FleetErrors.InvalidState458A59);

        Status = VehicleStatus.Published;
        HiddenByAdmin = false;
        _approvals.Add(new VehicleApproval(Guid.CreateVersion7(), Id, adminId, ApprovalDecision.Approved, null, DateTimeOffset.UtcNow));

        return Result.Success();
    }

    public Result Reject(Guid adminId, string reason)
    {
        if (Status != VehicleStatus.PendingApproval)
            return Result.Failure(FleetErrors.InvalidState921A2E);

        Status = VehicleStatus.Draft;
        _approvals.Add(new VehicleApproval(Guid.CreateVersion7(), Id, adminId, ApprovalDecision.Rejected, reason, DateTimeOffset.UtcNow));

        return Result.Success();
    }

    public Result Unpublish(Guid adminId, string reason)
    {
        if (Status != VehicleStatus.Published)
            return Result.Failure(FleetErrors.InvalidState6745DC);

        Status = VehicleStatus.Hidden;
        HiddenByAdmin = true;
        _approvals.Add(new VehicleApproval(Guid.CreateVersion7(), Id, adminId, ApprovalDecision.Rejected, $"Unpublished by admin: {reason}", DateTimeOffset.UtcNow));

        return Result.Success();
    }

    public Result Hide()
    {
        if (Status != VehicleStatus.Published)
            return Result.Failure(FleetErrors.InvalidStateB3AEDE);

        Status = VehicleStatus.Hidden;
        return Result.Success();
    }

    public Result Show()
    {
        if (Status != VehicleStatus.Hidden)
            return Result.Failure(FleetErrors.InvalidState756344);

        if (HiddenByAdmin)
            return Result.Failure(FleetErrors.HiddenByAdmin);

        Status = VehicleStatus.Published;
        return Result.Success();
    }
}
