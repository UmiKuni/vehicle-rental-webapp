using CarRental.BuildingBlocks.Domain;
using CarRental.Modules.Users.Contracts.Enums;

namespace CarRental.Modules.Users.Entities;

public class IdentityDocument : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public DocumentType Type { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public DocumentVerificationStatus VerificationStatus { get; private set; }
    public string? RejectReason { get; private set; }

    private IdentityDocument() { }

    internal IdentityDocument(Guid id, Guid userId, DocumentType type, string storageKey)
    {
        Id = id;
        UserId = userId;
        Type = type;
        StorageKey = storageKey;
        VerificationStatus = DocumentVerificationStatus.Pending;
        RejectReason = null;
    }

    internal void Verify(bool ok, string? reason)
    {
        VerificationStatus = ok ? DocumentVerificationStatus.Verified : DocumentVerificationStatus.Rejected;
        RejectReason = ok ? null : reason;
    }
}
