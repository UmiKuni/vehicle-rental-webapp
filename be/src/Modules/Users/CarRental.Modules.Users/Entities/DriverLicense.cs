using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Users.Enums;

namespace CarRental.Modules.Users.Entities;

public class DriverLicense : Entity<Guid>
{
    public string Number { get; private set; } = string.Empty;
    public LicenseClass Class { get; private set; }
    public DateOnly ExpiresOn { get; private set; }
    public FileReference Image { get; private set; } = null!;
    public DocumentStatus Status { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? RejectReason { get; private set; }

    private DriverLicense()
    {
    }

    private DriverLicense(
        Guid id,
        string number,
        LicenseClass licenseClass,
        DateOnly expiresOn,
        FileReference image) : base(id)
    {
        Number = number;
        Class = licenseClass;
        ExpiresOn = expiresOn;
        Image = image;
        Status = DocumentStatus.Pending;
    }

    public static DriverLicense CreatePending(
        string number,
        LicenseClass licenseClass,
        DateOnly expiresOn,
        FileReference image)
    {
        Guard.AgainstNullOrWhiteSpace(number, nameof(number), "Driver license number is required.");
        Guard.AgainstNull(image, nameof(image), "Driver license image is required.");
        Guard.Against(!image.IsPrivate, "license.image_must_be_private", "Driver license image must be marked private.");

        return new DriverLicense(Guid.CreateVersion7(), number.Trim(), licenseClass, expiresOn, image);
    }

    public void Approve(Guid adminId, DateTimeOffset now)
    {
        Guard.Against(adminId == Guid.Empty, "license.invalid_admin", "Admin identifier must not be empty.");
        Status = DocumentStatus.Verified;
        ReviewedBy = adminId;
        ReviewedAt = now;
        RejectReason = null;
    }

    public void Reject(Guid adminId, string reason, DateTimeOffset now)
    {
        Guard.Against(adminId == Guid.Empty, "license.invalid_admin", "Admin identifier must not be empty.");
        Guard.AgainstNullOrWhiteSpace(reason, nameof(reason), "Rejection reason is required.");
        Status = DocumentStatus.Rejected;
        ReviewedBy = adminId;
        ReviewedAt = now;
        RejectReason = reason.Trim();
    }
}
