namespace CarRental.Modules.Users.Contracts.Dtos;

public sealed record UserProfileDto(
    Guid UserId,
    string Email,
    string Phone,
    string FullName,
    string Address);
