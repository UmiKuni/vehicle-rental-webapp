using CarRental.BuildingBlocks.Domain.ValueObjects;

namespace CarRental.Modules.Bookings.Entities;

public readonly record struct CancellationOutcome(Money PenaltyAmount, Money RefundableAmount);
