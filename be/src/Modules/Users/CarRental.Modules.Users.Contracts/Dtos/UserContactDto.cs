namespace CarRental.Modules.Users.Contracts.Dtos;

public sealed record UserContactDto(
    Guid UserId,
    string Email,
    string Phone,
    string FullName);
