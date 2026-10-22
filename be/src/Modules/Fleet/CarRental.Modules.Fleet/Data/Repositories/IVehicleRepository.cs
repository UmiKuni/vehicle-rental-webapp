using CarRental.Modules.Fleet.Entities;
using CarRental.Modules.Fleet.ValueObjects;

namespace CarRental.Modules.Fleet.Data.Repositories;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(VehicleId id, CancellationToken ct = default);
    Task<Vehicle?> GetByLicensePlateAsync(LicensePlate plate, CancellationToken ct = default);
    Task<IReadOnlyList<Vehicle>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default);
    Task AddAsync(Vehicle vehicle, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
