using CarRental.Modules.Users.Errors;
using CarRental.BuildingBlocks.Auth;
using CarRental.BuildingBlocks.Redis;
using CarRental.BuildingBlocks.Storage;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Users.Contracts;
using CarRental.Modules.Users.Contracts.Dtos;
using CarRental.Modules.Users.Contracts.Enums;
using CarRental.Modules.Users.Data;
using CarRental.Modules.Users.Entities;
using System.Text.RegularExpressions;
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
    private readonly TimeProvider _clock;
    private readonly IImageStorage _imageStorage;
    private readonly ITokenRevocationStore? _tokenRevocationStore;
    private readonly ICacheService? _cacheService;

    public UsersApiService(
        UsersDbContext dbContext,
        PasswordHasher<object> passwordHasher,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        TimeProvider clock,
        IImageStorage imageStorage,
        ITokenRevocationStore? tokenRevocationStore = null,
        ICacheService? cacheService = null)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
        _clock = clock;
        _imageStorage = imageStorage;
        _tokenRevocationStore = tokenRevocationStore;
        _cacheService = cacheService;
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
            return Result.Failure<AuthResultDto>(UsersErrors.InvalidEmail);
        if (string.IsNullOrWhiteSpace(dto.Phone))
            return Result.Failure<AuthResultDto>(UsersErrors.InvalidPhone);
        if (string.IsNullOrWhiteSpace(dto.Password))
            return Result.Failure<AuthResultDto>(UsersErrors.InvalidPassword);

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
            return Result.Failure<AuthResultDto>(UsersErrors.CredentialsRequired);

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == dto.Identifier || u.Phone == dto.Identifier, ct);
        if (user is null)
            return Result.Failure<AuthResultDto>(UsersErrors.InvalidCredentials);
        if (user.Status == Contracts.Enums.UserStatus.Banned)
            return Result.Failure<AuthResultDto>(UsersErrors.BannedAccount);

        var verification = _passwordHasher.VerifyHashedPassword(new object(), user.PasswordHash, dto.Password);
        if (verification == PasswordVerificationResult.Failed)
            return Result.Failure<AuthResultDto>(UsersErrors.InvalidCredentials);

        return await IssueTokensAsync(user, null, ct);
    }

    public async Task<Result<AuthResultDto>> RefreshAsync(string refreshToken, string? deviceInfo = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result.Failure<AuthResultDto>(UsersErrors.RefreshTokenRequired);

        var hash = _tokenService.HashRefreshToken(refreshToken);
        var existing = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (existing is null || !existing.IsActiveAt(_clock.GetUtcNow()))
            return Result.Failure<AuthResultDto>(UsersErrors.RefreshTokenExpired);

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == UserId.From(existing.UserId), ct);
        if (user is null || user.Status == Contracts.Enums.UserStatus.Banned)
            return Result.Failure<AuthResultDto>(UsersErrors.RefreshTokenInvalid);

        return await IssueTokensAsync(user, existing, ct, deviceInfo);
    }

    public Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default)
        => LogoutAsync(refreshToken, null, null, ct);

    public async Task<Result> LogoutAsync(
        string refreshToken,
        string? accessTokenJti,
        DateTimeOffset? accessTokenExpiresAt,
        CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var hash = _tokenService.HashRefreshToken(refreshToken);
            var existing = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
            if (existing is not null && existing.IsActiveAt(now))
            {
                existing.Revoke(null, now);
                await _dbContext.SaveChangesAsync(ct);
            }
        }

        if (!string.IsNullOrWhiteSpace(accessTokenJti) && _tokenRevocationStore is not null)
        {
            var remaining = accessTokenExpiresAt.HasValue
                ? accessTokenExpiresAt.Value - now + TimeSpan.FromSeconds(30)
                : TimeSpan.FromMinutes(_jwtOptions.AccessTokenMinutes);

            if (remaining > TimeSpan.Zero)
                await _tokenRevocationStore.RevokeJtiAsync(accessTokenJti, remaining, ct);
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
            _clock.GetUtcNow());

        if (replacedToken is not null)
            replacedToken.Revoke(refreshEntity.Id, _clock.GetUtcNow());

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

        if (_tokenRevocationStore is not null)
        {
            var cutoffTtl = TimeSpan.FromMinutes(_jwtOptions.AccessTokenMinutes) + TimeSpan.FromMinutes(1);
            await _tokenRevocationStore.SetTokensValidAfterAsync(userId, _clock.GetUtcNow(), cutoffTtl, ct);
        }

        if (_cacheService is not null)
            await _cacheService.RemoveAsync(UserAuthCacheKeys.AuthSnapshot(userId), ct);

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

        if (_cacheService is not null)
            await _cacheService.RemoveAsync(UserAuthCacheKeys.AuthSnapshot(userId), ct);

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
        IEnumerable<AdminUserDto> source = dbUsers.Select(MapToAdminUserDto);

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

        if (!string.IsNullOrWhiteSpace(dto?.DocumentNumber))
        {
            var docNum = dto.DocumentNumber.Trim();
            if (!Regex.IsMatch(docNum, @"^\d{9,12}$"))
                return Result.Failure<UserVerificationStatusDto>(UsersErrors.InvalidDocumentNumber);
        }

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

        if (!string.IsNullOrWhiteSpace(dto?.StorageKey))
        {
            var addResult = user.AddIdentityDocument(DocumentType.CCCD, dto.StorageKey.Trim());
            if (addResult.IsFailure)
                return Result.Failure<UserVerificationStatusDto>(addResult.Error);
        }
        else
        {
            var hasCccd = user.IdentityDocuments.Any(d => d.Type == DocumentType.CCCD);
            if (!hasCccd)
                return Result.Failure<UserVerificationStatusDto>(UsersErrors.DocumentMissing);
        }

        await _dbContext.SaveChangesAsync(ct);

        if (_cacheService is not null)
            await _cacheService.RemoveAsync(UserAuthCacheKeys.AuthSnapshot(userId), ct);

        return Result.Success(MapToVerificationStatusDto(user));
    }

    public async Task<Result<UserVerificationStatusDto>> VerifyDriverLicenseAsync(Guid userId, VerifyDriverLicenseDto? dto = null, CancellationToken ct = default)
    {
        var userIdValue = UserId.From(userId);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userIdValue, ct);
        if (user is null)
            return Result.Failure<UserVerificationStatusDto>(UsersErrors.NotFound);

        if (!string.IsNullOrWhiteSpace(dto?.LicenseNumber))
        {
            var licNum = dto.LicenseNumber.Trim();
            if (!Regex.IsMatch(licNum, @"^\d{12}$"))
                return Result.Failure<UserVerificationStatusDto>(UsersErrors.InvalidLicenseNumber);
        }

        if (!string.IsNullOrWhiteSpace(dto?.StorageKey))
        {
            var addResult = user.AddIdentityDocument(DocumentType.GPLX, dto.StorageKey.Trim());
            if (addResult.IsFailure)
                return Result.Failure<UserVerificationStatusDto>(addResult.Error);
        }
        else
        {
            var hasGplx = user.IdentityDocuments.Any(d => d.Type == DocumentType.GPLX);
            if (!hasGplx)
                return Result.Failure<UserVerificationStatusDto>(UsersErrors.DocumentMissing);
        }

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success(MapToVerificationStatusDto(user));
    }

    public async Task<Result<UserVerificationStatusDto>> ReviewIdentityDocumentAsync(
        Guid userId,
        Guid documentId,
        Guid adminId,
        ReviewIdentityDocumentDto dto,
        CancellationToken ct = default)
    {
        var userIdValue = UserId.From(userId);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userIdValue, ct);
        if (user is null)
            return Result.Failure<UserVerificationStatusDto>(UsersErrors.NotFound);

        if (!dto.Approved && string.IsNullOrWhiteSpace(dto.RejectReason))
            return Result.Failure<UserVerificationStatusDto>(UsersErrors.RejectReasonRequired);

        var verifyResult = user.VerifyDocument(documentId, adminId, dto.Approved, dto.RejectReason?.Trim());
        if (verifyResult.IsFailure)
            return Result.Failure<UserVerificationStatusDto>(verifyResult.Error);

        await _dbContext.SaveChangesAsync(ct);

        if (_cacheService is not null)
            await _cacheService.RemoveAsync(UserAuthCacheKeys.AuthSnapshot(userId), ct);

        return Result.Success(MapToVerificationStatusDto(user));
    }

    public async Task<Result<IReadOnlyList<PendingVerificationDto>>> GetPendingVerificationsAsync(CancellationToken ct = default)
    {
        var dbUsers = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.IdentityDocuments.Any(d => d.VerificationStatus == DocumentVerificationStatus.Pending))
            .ToListAsync(ct);

        var list = new List<PendingVerificationDto>();
        foreach (var user in dbUsers)
        {
            foreach (var doc in user.IdentityDocuments.Where(d => d.VerificationStatus == DocumentVerificationStatus.Pending))
            {
                var urlResult = _imageStorage.GetUrl(doc.StorageKey, StorageVisibility.Private, TimeSpan.FromMinutes(15));
                list.Add(new PendingVerificationDto(
                    user.Id.Value,
                    user.Email,
                    user.Phone,
                    user.CustomerProfile?.FullName ?? string.Empty,
                    doc.Id,
                    doc.Type,
                    doc.StorageKey,
                    doc.VerificationStatus,
                    urlResult.IsSuccess ? urlResult.Value : null));
            }
        }

        return Result.Success<IReadOnlyList<PendingVerificationDto>>(list);
    }

    public async Task<Result<UserVerificationStatusDto>> UploadIdentityDocumentAsync(
        Guid userId,
        DocumentType type,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == UserId.From(userId), ct);
        if (user is null)
            return Result.Failure<UserVerificationStatusDto>(UsersErrors.NotFound);

        var uploadResult = await _imageStorage.UploadAsync(
            new UploadRequest(
                content,
                fileName,
                contentType,
                $"identity/{userId:N}",
                StorageVisibility.Private,
                AllowPdf: true),
            ct);
        if (uploadResult.IsFailure)
            return Result.Failure<UserVerificationStatusDto>(uploadResult.Error);

        var addResult = user.AddIdentityDocument(type, uploadResult.Value.StorageKey);
        if (addResult.IsFailure)
        {
            await _imageStorage.DeleteAsync(uploadResult.Value.StorageKey, StorageVisibility.Private, ct);
            return Result.Failure<UserVerificationStatusDto>(addResult.Error);
        }

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success(MapToVerificationStatusDto(user));
    }

    private IdentityDocumentDto MapToDocumentDto(IdentityDocument doc)
    {
        var urlResult = _imageStorage.GetUrl(
            doc.StorageKey,
            StorageVisibility.Private,
            TimeSpan.FromMinutes(5));

        return new(
            doc.Id,
            doc.Type,
            doc.StorageKey,
            doc.VerificationStatus,
            doc.RejectReason,
            urlResult.IsSuccess ? urlResult.Value : null);
    }

    private UserVerificationStatusDto MapToVerificationStatusDto(User user) =>
        new(
            user.Id.Value,
            user.IsIdVerified,
            user.IsDriverLicenseVerified,
            user.IsVerified,
            user.IdentityDocuments.Select(MapToDocumentDto).ToList());

    private AdminUserDto MapToAdminUserDto(User user) =>
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

}
