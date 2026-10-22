namespace CarRental.Modules.Users.Contracts.Dtos;

public sealed record UserSummaryDto(
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    string? AvatarUrl,
    bool IsOwner,
    bool IsVerified);
