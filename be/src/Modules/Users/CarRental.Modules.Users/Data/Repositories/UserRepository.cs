using CarRental.Modules.Users.Entities;
using CarRental.Modules.Users.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Users.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly UsersDbContext _dbContext;

    public UserRepository(UsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserProfile?> GetByIdAsync(UserId id, CancellationToken ct = default)
    {
        return await _dbContext.UserProfiles
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<UserProfile?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await _dbContext.UserProfiles
            .FirstOrDefaultAsync(u => u.Email == normalized, ct);
    }

    public async Task AddAsync(UserProfile userProfile, CancellationToken ct = default)
    {
        await _dbContext.UserProfiles.AddAsync(userProfile, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _dbContext.SaveChangesAsync(ct);
    }
}
