using CarRental.BuildingBlocks.Domain;
using CarRental.Modules.Handover.Contracts.Enums;

namespace CarRental.Modules.Handover.Entities;

public class HandoverPhoto : Entity<Guid>
{
    public HandoverId HandoverId { get; private set; }
    public PhotoCorner Corner { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string FileHash { get; private set; } = string.Empty;
    public DateTimeOffset ClientCapturedAt { get; private set; }

    private HandoverPhoto() { }

    public HandoverPhoto(
        Guid id,
        HandoverId handoverId,
        PhotoCorner corner,
        string storageKey,
        string fileHash,
        DateTimeOffset clientCapturedAt)
    {
        Id = id;
        HandoverId = handoverId;
        Corner = corner;
        StorageKey = storageKey;
        FileHash = fileHash;
        ClientCapturedAt = clientCapturedAt;
    }
}
