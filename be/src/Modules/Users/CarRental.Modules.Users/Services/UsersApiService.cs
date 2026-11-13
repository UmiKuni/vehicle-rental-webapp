using CarRental.Modules.Users.Errors;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Users.Contracts;
using CarRental.Modules.Users.Contracts.Dtos;
using CarRental.Modules.Users.Contracts.Enums;
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

    public async Task<Result<PagedResult<AdminUserDto>>> GetAllUsersAsync(
        int page = 1,
        int pageSize = 10,
        string? search = null,
        string? role = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);

        var dbUsers = await _dbContext.Users.AsNoTracking().ToListAsync(ct);
        IEnumerable<AdminUserDto> source = dbUsers.Count > 0
            ? dbUsers.Select(MapToAdminUserDto)
            : GetMockAdminUsers();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            source = source.Where(u =>
                u.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                u.Phone.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                u.FullName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleFilter = role.Trim();
            source = source.Where(u => string.Equals(u.Role, roleFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusFilter = status.Trim();
            source = source.Where(u => string.Equals(u.Status, statusFilter, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = source.OrderBy(u => u.Email, StringComparer.OrdinalIgnoreCase).ToList();
        var items = filtered
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();

        return Result.Success(new PagedResult<AdminUserDto>(items, filtered.Count, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<UserVerificationStatusDto>> GetVerificationStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var userIdValue = UserId.From(userId);
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userIdValue, ct);
        if (user is null)
            return Result.Failure<UserVerificationStatusDto>(UsersErrors.NotFound);

        return Result.Success(MapToVerificationStatusDto(user));
    }

    public async Task<Result<UserVerificationStatusDto>> VerifyIdAsync(Guid userId, VerifyIdDocumentDto? dto = null, CancellationToken ct = default)
    {
        var userIdValue = UserId.From(userId);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userIdValue, ct);
        if (user is null)
            return Result.Failure<UserVerificationStatusDto>(UsersErrors.NotFound);

        var storageKey = !string.IsNullOrWhiteSpace(dto?.StorageKey)
            ? dto.StorageKey.Trim()
            : !string.IsNullOrWhiteSpace(dto?.DocumentNumber)
                ? $"mock/cccd/{userId:N}_{dto.DocumentNumber.Trim()}.jpg"
                : $"mock/cccd/{userId:N}.jpg";

        var verifyResult = user.VerifyDocumentByTypeMock(DocumentType.CCCD, storageKey);
        if (verifyResult.IsFailure)
            return Result.Failure<UserVerificationStatusDto>(verifyResult.Error);

        if (!string.IsNullOrWhiteSpace(dto?.FullName) || user.CustomerProfile is null)
        {
            var fullName = string.IsNullOrWhiteSpace(dto?.FullName)
                ? "Verified Customer"
                : dto.FullName.Trim();
            var address = dto?.Address ?? user.CustomerProfile?.Address ?? string.Empty;
            var profileResult = user.UpdateProfile(fullName, address);
            if (profileResult.IsFailure)
                return Result.Failure<UserVerificationStatusDto>(profileResult.Error);
        }

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success(MapToVerificationStatusDto(user));
    }

    public async Task<Result<UserVerificationStatusDto>> VerifyDriverLicenseAsync(Guid userId, VerifyDriverLicenseDto? dto = null, CancellationToken ct = default)
    {
        var userIdValue = UserId.From(userId);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userIdValue, ct);
        if (user is null)
            return Result.Failure<UserVerificationStatusDto>(UsersErrors.NotFound);

        var storageKey = !string.IsNullOrWhiteSpace(dto?.StorageKey)
            ? dto.StorageKey.Trim()
            : !string.IsNullOrWhiteSpace(dto?.LicenseNumber)
                ? $"mock/gplx/{userId:N}_{dto.LicenseNumber.Trim()}.jpg"
                : dto?.LicenseClass is not null
                    ? $"mock/gplx/{userId:N}_{dto.LicenseClass.Value}.jpg"
                    : $"mock/gplx/{userId:N}.jpg";

        var verifyResult = user.VerifyDocumentByTypeMock(DocumentType.GPLX, storageKey);
        if (verifyResult.IsFailure)
            return Result.Failure<UserVerificationStatusDto>(verifyResult.Error);

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success(MapToVerificationStatusDto(user));
    }

    private static IdentityDocumentDto MapToDocumentDto(IdentityDocument doc) =>
        new(doc.Id, doc.Type, doc.StorageKey, doc.VerificationStatus, doc.RejectReason);

    private static UserVerificationStatusDto MapToVerificationStatusDto(User user) =>
        new(
            user.Id.Value,
            user.IsIdVerified,
            user.IsDriverLicenseVerified,
            user.IsVerified,
            user.IdentityDocuments.Select(MapToDocumentDto).ToList());

    private static AdminUserDto MapToAdminUserDto(User user) =>
        new(
            user.Id.Value,
            user.Email,
            user.Phone,
            user.CustomerProfile?.FullName ?? string.Empty,
            user.CustomerProfile?.Address ?? string.Empty,
            user.Role.ToString(),
            user.Status.ToString(),
            user.IsIdVerified,
            user.IsDriverLicenseVerified,
            user.IsVerified,
            user.IdentityDocuments.Select(MapToDocumentDto).ToList());

    private static IReadOnlyList<AdminUserDto> GetMockAdminUsers() =>
    [
        new(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            "admin.seed@example.com",
            "0900000100",
            "Platform Administrator",
            "District 1, Ho Chi Minh City",
            UserRole.Admin.ToString(),
            UserStatus.Active.ToString(),
            true,
            true,
            true,
            []),
        new(
            Guid.Parse("00000000-0000-0000-0000-000000000101"),
            "owner.seed@example.com",
            "0900000101",
            "Seed Vehicle Owner",
            "1 Nguyen Hue, Ho Chi Minh City",
            UserRole.Owner.ToString(),
            UserStatus.Active.ToString(),
            true,
            true,
            true,
            [
                new(Guid.Parse("10000000-0000-0000-0000-000000000001"), DocumentType.CCCD, "mock/cccd/seed-owner.jpg", DocumentVerificationStatus.Verified, null),
                new(Guid.Parse("10000000-0000-0000-0000-000000000002"), DocumentType.GPLX, "mock/gplx/seed-owner.jpg", DocumentVerificationStatus.Verified, null)
            ]),
        new(
            Guid.Parse("00000000-0000-0000-0000-000000000102"),
            "customer.seed@example.com",
            "0900000102",
            "Seed Customer",
            "2 Le Loi, Ho Chi Minh City",
            UserRole.Customer.ToString(),
            UserStatus.Active.ToString(),
            false,
            false,
            false,
            [])
    ];
}
