using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Users.Contracts;
using CarRental.Modules.Users.Contracts.Dtos;
using CarRental.Modules.Users.Data.Repositories;
using CarRental.Modules.Users.Enums;
using CarRental.Modules.Users.ValueObjects;

namespace CarRental.Modules.Users.Services;

public class UsersApiService : IUsersApi
{
    private readonly IUserRepository _userRepository;
    private readonly TimeProvider _timeProvider;

    public UsersApiService(IUserRepository userRepository, TimeProvider timeProvider)
    {
        _userRepository = userRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Result<RenterEligibilityDto>> GetRenterEligibilityAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(new UserId(userId), ct);
        if (user == null)
        {
            return Result<RenterEligibilityDto>.Failure(new Error("user.not_found", $"User with ID '{userId}' was not found."));
        }

        var now = _timeProvider.GetUtcNow();
        var eligibility = user.GetRenterEligibility(now);
        return Result<RenterEligibilityDto>.Success(eligibility);
    }

    public async Task<Result<UserSummaryDto>> GetUserSummaryAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(new UserId(userId), ct);
        if (user == null)
        {
            return Result<UserSummaryDto>.Failure(new Error("user.not_found", $"User with ID '{userId}' was not found."));
        }

        var isOwner = user.Roles.Contains(UserRole.Owner);
        var isVerified = user.IdentityDocument?.Status == DocumentStatus.Verified;

        var summary = new UserSummaryDto(
            user.Id.Value,
            user.FullName,
            user.Email,
            user.PhoneNumber?.E164,
            user.Avatar?.Url,
            isOwner,
            isVerified);

        return Result<UserSummaryDto>.Success(summary);
    }

    public async Task<bool> IsVerifiedOwnerAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(new UserId(userId), ct);
        if (user == null)
        {
            return false;
        }

        var isOwner = user.Roles.Contains(UserRole.Owner);
        var isVerified = user.IdentityDocument?.Status == DocumentStatus.Verified;
        return isOwner && isVerified && user.Status == UserStatus.Active;
    }
}
