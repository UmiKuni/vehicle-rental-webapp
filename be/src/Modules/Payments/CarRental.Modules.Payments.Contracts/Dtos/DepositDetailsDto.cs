using CarRental.Modules.Payments.Contracts.Enums;

namespace CarRental.Modules.Payments.Contracts.Dtos;

public sealed record DepositDetailsDto(
    Guid DepositId,
    Guid BookingId,
    decimal Amount,
    DepositStatus Status,
    decimal DeductedAmount,
    bool IsFrozen,
    DateTimeOffset? ReleaseAfter,
    DateTimeOffset? SettledAt);

