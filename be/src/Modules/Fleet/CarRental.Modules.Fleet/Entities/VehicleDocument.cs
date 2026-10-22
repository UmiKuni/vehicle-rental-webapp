using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Fleet.Enums;

namespace CarRental.Modules.Fleet.Entities;

public class VehicleDocument : Entity<Guid>
{
    public VehicleDocumentType Type { get; private set; }
    public FileReference File { get; private set; } = null!;
    public DateOnly? ExpiresOn { get; private set; }
    public DocumentStatus Status { get; private set; }

    private VehicleDocument()
    {
    }

    private VehicleDocument(
        Guid id,
        VehicleDocumentType type,
        FileReference file,
        DateOnly? expiresOn) : base(id)
    {
        Type = type;
        File = file;
        ExpiresOn = expiresOn;
        Status = DocumentStatus.Pending;
    }

    public static VehicleDocument Create(
        VehicleDocumentType type,
        FileReference file,
        DateOnly? expiresOn = null)
    {
        Guard.AgainstNull(file, nameof(file), "Document file is required.");
        Guard.Against(!file.IsPrivate, "document.public_forbidden", "Vehicle documents must be marked private.");

        return new VehicleDocument(Guid.CreateVersion7(), type, file, expiresOn);
    }

    public void Verify()
    {
        Status = DocumentStatus.Verified;
    }

    public void Reject()
    {
        Status = DocumentStatus.Rejected;
    }
}
