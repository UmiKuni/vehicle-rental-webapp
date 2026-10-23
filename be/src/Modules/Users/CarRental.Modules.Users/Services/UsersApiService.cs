using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Users.Contracts;
using CarRental.Modules.Users.Contracts.Dtos;
using CarRental.Modules.Users.Data;
using CarRental.Modules.Users.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Users.Services;

public class UsersApiService : IUsersApi
{
    private readonly UsersDbContext _dbContext;
    private readonly PasswordHasher<object> _passwordHasher;

    public UsersApiService(UsersDbContext dbContext, PasswordHasher<object> passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<BookingEligibilityDto>> GetBookingEligibilityAsync(Guid userId, CancellationToken ct = default)
    {
        var userIdValue = UserId.From(userId);
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userIdValue, ct);
        if (user is null)
            return Result.Failure<BookingEligibilityDto>(new Error("users.not_found", "User not found"));
        return Result.Success(user.GetBookingEligibility());
    }

    public async Task<Result<UserContactDto>> GetContactAsync(Guid userId, CancellationToken ct = default)
    {
        var userIdValue = UserId.From(userId);
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userIdValue, ct);
        if (user is null)
            return Result.Failure<UserContactDto>(new Error("users.not_found", "User not found"));
        var fullName = user.CustomerProfile?.FullName ?? string.Empty;
        return Result.Success(new UserContactDto(user.Id.Value, user.Email, user.Phone, fullName));
    }

    public async Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var userIdValue = UserId.From(userId);
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userIdValue, ct);
        if (user is null)
            return Result.Failure<UserProfileDto>(new Error("users.not_found", "User not found"));

        return Result.Success(new UserProfileDto(
            user.Id.Value,
            user.Email,
            user.Phone,
            user.CustomerProfile?.FullName ?? string.Empty,
            user.CustomerProfile?.Address ?? string.Empty));
    }

    public async Task<Result<AuthResultDto>> RegisterAsync(RegisterUserDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            return Result.Failure<AuthResultDto>(new Error("users.invalid_email", "Email cannot be empty"));
        if (string.IsNullOrWhiteSpace(dto.Phone))
            return Result.Failure<AuthResultDto>(new Error("users.invalid_phone", "Phone cannot be empty"));
        if (string.IsNullOrWhiteSpace(dto.Password))
            return Result.Failure<AuthResultDto>(new Error("users.invalid_password", "Password cannot be empty"));

        var exists = await _dbContext.Users.AnyAsync(u => u.Email == dto.Email || u.Phone == dto.Phone, ct);
        if (exists)
            return Result.Failure<AuthResultDto>(new Error("users.duplicate", "Email or phone already in use"));

        var passwordHash = _passwordHasher.HashPassword(new object(), dto.Password);
        var createResult = User.Register(dto.Email, dto.Phone, passwordHash);
        if (createResult.IsFailure)
            return Result.Failure<AuthResultDto>(createResult.Error);

        var user = createResult.Value;
        await _dbContext.Users.AddAsync(user, ct);
        await _dbContext.SaveChangesAsync(ct);

        var token = Guid.NewGuid().ToString(); // TODO: replace with proper JWT generation
        return Result.Success(new AuthResultDto(user.Id.Value, token));
    }

    public async Task<Result<AuthResultDto>> LoginAsync(LoginUserDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Identifier) || string.IsNullOrWhiteSpace(dto.Password))
            return Result.Failure<AuthResultDto>(new Error("users.invalid_credentials", "Identifier and password required"));

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == dto.Identifier || u.Phone == dto.Identifier, ct);
        if (user is null)
            return Result.Failure<AuthResultDto>(new Error("users.not_found", "User not found"));

        var verification = _passwordHasher.VerifyHashedPassword(new object(), user.PasswordHash, dto.Password);
        if (verification == PasswordVerificationResult.Failed)
            return Result.Failure<AuthResultDto>(new Error("users.invalid_credentials", "Invalid password"));

        var token = Guid.NewGuid().ToString(); // TODO: replace with proper JWT generation
        return Result.Success(new AuthResultDto(user.Id.Value, token));
    }

    public async Task<Result> UpdateProfileAsync(Guid userId, UpdateProfileDto dto, CancellationToken ct = default)
    {
        var userIdValue = UserId.From(userId);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userIdValue, ct);
        if (user is null)
            return Result.Failure(new Error("users.not_found", "User not found"));

        var result = user.UpdateProfile(dto.FullName, dto.Address);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> BanAsync(Guid userId, Guid adminId, CancellationToken ct = default)
    {
        var userIdValue = UserId.From(userId);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userIdValue, ct);
        if (user is null)
            return Result.Failure(new Error("users.not_found", "User not found"));

        var result = user.Ban(adminId, "Banned by admin");
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UnbanAsync(Guid userId, Guid adminId, CancellationToken ct = default)
    {
        var userIdValue = UserId.From(userId);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userIdValue, ct);
        if (user is null)
            return Result.Failure(new Error("users.not_found", "User not found"));

        var result = user.Unban(adminId);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }
}
