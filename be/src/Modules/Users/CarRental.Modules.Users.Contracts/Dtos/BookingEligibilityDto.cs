namespace CarRental.Modules.Users.Contracts.Dtos;

public sealed record BookingEligibilityDto(
    bool IsEligible,
    string? IneligibilityReason);
