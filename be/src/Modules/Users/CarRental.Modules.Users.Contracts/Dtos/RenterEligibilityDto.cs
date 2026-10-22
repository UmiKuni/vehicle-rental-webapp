namespace CarRental.Modules.Users.Contracts.Dtos;

public sealed record RenterEligibilityDto(
    bool IsEligible,
    IReadOnlyList<string> IneligibilityReasons);
