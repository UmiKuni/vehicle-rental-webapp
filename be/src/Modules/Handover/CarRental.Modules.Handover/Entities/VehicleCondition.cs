using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Handover.Entities;

public class VehicleCondition : Entity<Guid>
{
    public Guid HandoverId { get; private set; }
    public int Odometer { get; private set; }
    public int FuelLevel { get; private set; }
    public string? DamageNotes { get; private set; }

    private VehicleCondition() { }

    public VehicleCondition(Guid handoverId, int odometer, int fuelLevel, string? damageNotes)
    {
        Id = handoverId;
        HandoverId = handoverId;
        Odometer = odometer;
        FuelLevel = fuelLevel;
        DamageNotes = damageNotes;
    }
}
