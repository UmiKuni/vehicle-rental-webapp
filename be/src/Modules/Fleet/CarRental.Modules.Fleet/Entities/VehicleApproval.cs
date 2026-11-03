using CarRental.BuildingBlocks.Domain;
using CarRental.Modules.Fleet.Contracts.Enums;

namespace CarRental.Modules.Fleet.Entities;

public class VehicleApproval : Entity<Guid>
{
    public VehicleId VehicleId { get; private set; }
    public Guid AdminId { get; private set; }
    public ApprovalDecision Decision { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private VehicleApproval() { }

    internal VehicleApproval(Guid id, VehicleId vehicleId, Guid adminId, ApprovalDecision decision, string? reason, DateTimeOffset createdAt)
    {
        Id = id;
        VehicleId = vehicleId;
        AdminId = adminId;
        Decision = decision;
        Reason = reason;
        CreatedAt = createdAt;
    }
}
