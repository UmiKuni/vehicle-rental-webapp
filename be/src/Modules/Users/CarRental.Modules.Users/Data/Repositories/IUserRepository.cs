using CarRental.Modules.Users.Entities;
using CarRental.Modules.Users.ValueObjects;

namespace CarRental.Modules.Users.Data.Repositories;

public interface IUserRepository
{
    Task<UserProfile?> GetByIdAsync(UserId id, CancellationToken ct = default);
    Task<UserProfile?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(UserProfile userProfile, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
