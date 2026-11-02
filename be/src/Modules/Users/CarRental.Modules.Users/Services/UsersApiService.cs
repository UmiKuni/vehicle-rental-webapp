using CarRental.Modules.Users.Errors;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Users.Contracts;
using CarRental.Modules.Users.Contracts.Dtos;
using CarRental.Modules.Users.Data;
using CarRental.Modules.Users.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CarRental.Modules.Users.Services;

public class UsersApiService : IUsersApi
{
    private readonly UsersDbContext _dbContext;
    private readonly PasswordHasher<object> _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;

    public UsersApiService(
        UsersDbContext dbContext,
        PasswordHasher<object> passwordHasher,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<Result<BookingEligibilityDto>> GetBookingEligibilityAsync(Guid userId, CancellationToken ct = default)
    {
        var userIdValue = UserId.From(userId);
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userIdValue, ct);
        if (user is null)
            return Result.Failure<BookingEligibilityDto>(UsersErrors.NotFound);
        return Result.Success(user.GetBookingEligibility());
    }

    public async Task<Result<UserContactDto>> GetContactAsync(Guid userId, CancellationToken ct = default)
    {
        var userIdValue = UserId.From(userId);
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userIdValue, ct);
        if (user is null)
            return Result.Failure<UserContactDto>(UsersErrors.NotFound);
        var fullName = user.CustomerProfile?.FullName ?? string.Empty;
        return Result.Success(new UserContactDto(user.Id.Value, user.Email, user.Phone, fullName));
    }

    public async Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var userIdValue = UserId.From(userId);
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userIdValue, ct);
        if (user is null)
            return Result.Failure<UserProfileDto>(UsersErrors.NotFound);

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
            return Result.Failure<AuthResultDto>(UsersErrors.InvalidEmail4D9514);
        if (string.IsNullOrWhiteSpace(dto.Phone))
            return Result.Failure<AuthResultDto>(UsersErrors.InvalidPhone8199CF);
        if (string.IsNullOrWhiteSpace(dto.Password))
            return Result.Failure<AuthResultDto>(UsersErrors.InvalidPassword4BB6F3);

        var exists = await _dbContext.Users.AnyAsync(u => u.Email == dto.Email || u.Phone == dto.Phone, ct);
        if (exists)
            return Result.Failure<AuthResultDto>(UsersErrors.Duplicate);

        var passwordHash = _passwordHasher.HashPassword(new object(), dto.Password);
        var createResult = User.Register(dto.Email, dto.Phone, passwordHash);
        if (createResult.IsFailure)
            return Result.Failure<AuthResultDto>(createResult.Error);

        var user = createResult.Value;
        await _dbContext.Users.AddAsync(user, ct);
        await _dbContext.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, null, ct);
    }

    public async Task<Result<AuthResultDto>> LoginAsync(LoginUserDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Identifier) || string.IsNullOrWhiteSpace(dto.Password))
            return Result.Failure<AuthResultDto>(UsersErrors.InvalidCredentials2B2E58);

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == dto.Identifier || u.Phone == dto.Identifier, ct);
        if (user is null)
            return Result.Failure<AuthResultDto>(UsersErrors.InvalidCredentialsE41784);
        if (user.Status == Contracts.Enums.UserStatus.Banned)
            return Result.Failure<AuthResultDto>(UsersErrors.Banned3C3263);

        var verification = _passwordHasher.VerifyHashedPassword(new object(), user.PasswordHash, dto.Password);
        if (verification == PasswordVerificationResult.Failed)
            return Result.Failure<AuthResultDto>(UsersErrors.InvalidCredentialsE41784);

        return await IssueTokensAsync(user, null, ct);
    }

    public async Task<Result<AuthResultDto>> RefreshAsync(string refreshToken, string? deviceInfo = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result.Failure<AuthResultDto>(UsersErrors.InvalidRefreshToken7F376E);

        var hash = _tokenService.HashRefreshToken(refreshToken);
        var existing = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (existing is null || !existing.IsActive)
            return Result.Failure<AuthResultDto>(UsersErrors.InvalidRefreshToken339042);

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == UserId.From(existing.UserId), ct);
        if (user is null || user.Status == Contracts.Enums.UserStatus.Banned)
            return Result.Failure<AuthResultDto>(UsersErrors.InvalidRefreshToken0C6350);

        return await IssueTokensAsync(user, existing, ct, deviceInfo);
    }

    public async Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result.Success();

        var hash = _tokenService.HashRefreshToken(refreshToken);
        var existing = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (existing is not null && existing.IsActive)
        {
            existing.Revoke();
            await _dbContext.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    private async Task<Result<AuthResultDto>> IssueTokensAsync(
        User user,
        RefreshToken? replacedToken,
        CancellationToken ct,
        string? deviceInfo = null)
    {
        var access = _tokenService.CreateAccessToken(user);
        var refresh = _tokenService.CreateRefreshToken();
        var refreshEntity = new RefreshToken(
            Guid.CreateVersion7(),
            user.Id.Value,
            refresh.Hash,
            refresh.ExpiresAt,
            deviceInfo,
            DateTimeOffset.UtcNow);

        if (replacedToken is not null)
            replacedToken.Revoke(refreshEntity.Id);

        await _dbContext.RefreshTokens.AddAsync(refreshEntity, ct);
        await _dbContext.SaveChangesAsync(ct);
        return Result.Success(new AuthResultDto(
            user.Id.Value,
            access.Token,
            access.ExpiresAt,
            refresh.Token,
            refresh.ExpiresAt));
    }

    public async Task<Result> UpdateProfileAsync(Guid userId, UpdateProfileDto dto, CancellationToken ct = default)
    {
        var userIdValue = UserId.From(userId);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userIdValue, ct);
        if (user is null)
            return Result.Failure(UsersErrors.NotFound);

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
            return Result.Failure(UsersErrors.NotFound);

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
            return Result.Failure(UsersErrors.NotFound);

        var result = user.Unban(adminId);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }
}
