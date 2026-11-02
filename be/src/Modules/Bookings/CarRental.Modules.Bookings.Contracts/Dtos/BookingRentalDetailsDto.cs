namespace CarRental.Modules.Bookings.Contracts.Dtos;

public sealed record BookingRentalDetailsDto(
    Guid BookingId,
    Guid CustomerId,
    Guid OwnerId,
    Guid VehicleId,
    string Status,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    decimal TotalRentAmount,
    decimal SecurityDepositAmount);

