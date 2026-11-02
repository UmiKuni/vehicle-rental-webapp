using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Users.Contracts.Dtos;

namespace CarRental.Modules.Users.Contracts;

public interface IUsersApi
{
    Task<Result<BookingEligibilityDto>> GetBookingEligibilityAsync(Guid userId, CancellationToken ct = default);
    Task<Result<UserContactDto>> GetContactAsync(Guid userId, CancellationToken ct = default);
    Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken ct = default);
    Task<Result<AuthResultDto>> RegisterAsync(RegisterUserDto dto, CancellationToken ct = default);
    Task<Result<AuthResultDto>> LoginAsync(LoginUserDto dto, CancellationToken ct = default);
    Task<Result<AuthResultDto>> RefreshAsync(string refreshToken, string? deviceInfo = null, CancellationToken ct = default);
    Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default);
    Task<Result> UpdateProfileAsync(Guid userId, UpdateProfileDto dto, CancellationToken ct = default);
    Task<Result> BanAsync(Guid userId, Guid adminId, CancellationToken ct = default);
    Task<Result> UnbanAsync(Guid userId, Guid adminId, CancellationToken ct = default);
}
