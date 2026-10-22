using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;

namespace CarRental.Modules.Fleet.Entities;

public class VehiclePhoto : Entity<Guid>
{
    public FileReference Photo { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public bool IsPrimary { get; private set; }

    private VehiclePhoto()
    {
    }

    private VehiclePhoto(Guid id, FileReference photo, int sortOrder, bool isPrimary) : base(id)
    {
        Photo = photo;
        SortOrder = sortOrder;
        IsPrimary = isPrimary;
    }

    public static VehiclePhoto Create(FileReference photo, int sortOrder = 0, bool isPrimary = false)
    {
        Guard.AgainstNull(photo, nameof(photo), "Photo reference is required.");
        Guard.Against(photo.IsPrivate, "photo.private_forbidden", "Vehicle photos must be public.");

        return new VehiclePhoto(Guid.CreateVersion7(), photo, sortOrder, isPrimary);
    }

    public void SetPrimary(bool isPrimary)
    {
        IsPrimary = isPrimary;
    }

    public void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
    }
}
