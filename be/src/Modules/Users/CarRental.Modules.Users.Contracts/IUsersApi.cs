using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Users.Contracts.Dtos;
using CarRental.Modules.Users.Contracts.Enums;

namespace CarRental.Modules.Users.Contracts;

public interface IUsersApi
{
    Task<Result<BookingEligibilityDto>> GetBookingEligibilityAsync(Guid userId, CancellationToken ct = default);
    Task<Result<UserContactDto>> GetContactAsync(Guid userId, CancellationToken ct = default);
    Task<Result<CurrentUserDto>> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
    Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken ct = default);
    Task<Result<AuthResultDto>> RegisterAsync(RegisterUserDto dto, CancellationToken ct = default);
    Task<Result<AuthResultDto>> LoginAsync(LoginUserDto dto, CancellationToken ct = default);
    Task<Result<AuthResultDto>> RefreshAsync(string refreshToken, string? deviceInfo = null, CancellationToken ct = default);
    Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default);
    Task<Result> LogoutAsync(
        string refreshToken,
        string? accessTokenJti,
        DateTimeOffset? accessTokenExpiresAt,
        CancellationToken ct = default);
    Task<Result> UpdateProfileAsync(Guid userId, UpdateProfileDto dto, CancellationToken ct = default);
    Task<Result> BanAsync(Guid userId, Guid adminId, CancellationToken ct = default);
    Task<Result> UnbanAsync(Guid userId, Guid adminId, CancellationToken ct = default);
    Task<Result<PagedResult<AdminUserDto>>> GetAllUsersAsync(
        int page = 1,
        int pageSize = 10,
        string? search = null,
        string? role = null,
        string? status = null,
        CancellationToken ct = default);
    Task<Result<UserVerificationStatusDto>> GetVerificationStatusAsync(Guid userId, CancellationToken ct = default);
    Task<Result<UserVerificationStatusDto>> VerifyIdAsync(Guid userId, VerifyIdDocumentDto? dto = null, CancellationToken ct = default);
    Task<Result<UserVerificationStatusDto>> VerifyDriverLicenseAsync(Guid userId, VerifyDriverLicenseDto? dto = null, CancellationToken ct = default);
    Task<Result<UserVerificationStatusDto>> UploadIdentityDocumentAsync(
        Guid userId,
        DocumentType type,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken ct = default);
    Task<Result<UserVerificationStatusDto>> ReviewIdentityDocumentAsync(
        Guid userId,
        Guid documentId,
        Guid adminId,
        ReviewIdentityDocumentDto dto,
        CancellationToken ct = default);
    Task<Result<IReadOnlyList<PendingVerificationDto>>> GetPendingVerificationsAsync(CancellationToken ct = default);
}
