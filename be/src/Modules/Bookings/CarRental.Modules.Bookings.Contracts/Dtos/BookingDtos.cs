namespace CarRental.Modules.Bookings.Contracts.Dtos;

public sealed record CreateBookingRequest(
    Guid VehicleId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string PickupLocation,
    bool IncludeInsurance,
    string? Notes);

public sealed record BookingLineItemDto(
    Guid Id,
    string Type,
    decimal Amount,
    string Description);

public sealed record BookingStatusHistoryDto(
    string Status,
    DateTimeOffset ChangedAt,
    string? Reason);

public sealed record BookingDetailDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    Guid OwnerId,
    Guid VehicleId,
    string VehiclePlate,
    string VehicleModel,
    string Status,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string PickupLocation,
    decimal TotalRentAmount,
    decimal SecurityDepositAmount,
    DateTimeOffset? HoldExpiresAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<BookingLineItemDto> LineItems,
    IReadOnlyList<BookingStatusHistoryDto> StatusHistories);

public sealed record BookingSummaryDto(
    Guid Id,
    Guid VehicleId,
    string VehicleModel,
    string VehiclePlate,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAt);

public sealed record RejectBookingRequest(string Reason);

public sealed record CancelBookingRequest(string Reason);

public sealed record AdminCancelBookingRequest(string Reason, string PenaltyParty);

public sealed record BookingCancellationResultDto(
    Guid BookingId,
    string Status,
    decimal TotalPaid,
    decimal PenaltyAmount,
    decimal RefundAmount,
    string CancellationReason,
    DateTimeOffset CancelledAt);
