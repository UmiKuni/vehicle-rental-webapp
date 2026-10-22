using CarRental.Modules.Fleet.Entities;
using CarRental.Modules.Fleet.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Fleet.Data.Repositories;

public class VehicleRepository : IVehicleRepository
{
    private readonly FleetDbContext _dbContext;

    public VehicleRepository(FleetDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Vehicle?> GetByIdAsync(VehicleId id, CancellationToken ct = default)
    {
        return await _dbContext.Vehicles
            .FirstOrDefaultAsync(v => v.Id == id, ct);
    }

    public async Task<Vehicle?> GetByLicensePlateAsync(LicensePlate plate, CancellationToken ct = default)
    {
        return await _dbContext.Vehicles
            .FirstOrDefaultAsync(v => v.LicensePlate.Value == plate.Value, ct);
    }

    public async Task<IReadOnlyList<Vehicle>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default)
    {
        return await _dbContext.Vehicles
            .Where(v => v.OwnerId == ownerId)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Vehicle vehicle, CancellationToken ct = default)
    {
        await _dbContext.Vehicles.AddAsync(vehicle, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _dbContext.SaveChangesAsync(ct);
    }
}
