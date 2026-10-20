using CarRental.BuildingBlocks.Domain;
using CarRental.Modules.Users.Contracts.Enums;

namespace CarRental.Modules.Users.Entities;

public class OwnerProfile : Entity<Guid>
{
    public Guid UserId => Id;
    public string TaxCode { get; private set; } = string.Empty;
    public OwnerApprovalStatus ApprovalStatus { get; private set; }
    public string? RejectReason { get; private set; }

    private OwnerProfile() { }

    internal OwnerProfile(Guid userId, string taxCode)
    {
        Id = userId;
        TaxCode = taxCode;
        ApprovalStatus = OwnerApprovalStatus.Pending;
        RejectReason = null;
    }

    internal void Resubmit(string taxCode)
    {
        TaxCode = taxCode;
        ApprovalStatus = OwnerApprovalStatus.Pending;
        RejectReason = null;
    }

    internal void Approve()
    {
        ApprovalStatus = OwnerApprovalStatus.Approved;
        RejectReason = null;
    }

    internal void Reject(string reason)
    {
        ApprovalStatus = OwnerApprovalStatus.Rejected;
        RejectReason = reason;
    }
}
