using CarRental.BuildingBlocks.Domain.ValueObjects;

namespace CarRental.Modules.Bookings.Entities;

internal readonly record struct CancellationOutcome(Money PenaltyAmount, Money RefundableAmount);
