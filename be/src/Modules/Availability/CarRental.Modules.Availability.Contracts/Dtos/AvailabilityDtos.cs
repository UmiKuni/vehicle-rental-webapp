using CarRental.Modules.Availability.Contracts.Enums;

namespace CarRental.Modules.Availability.Contracts.Dtos;

public sealed record AvailabilityCheckResultDto(
    Guid VehicleId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    bool IsAvailable,
    string? ConflictReason,
    IReadOnlyList<DateTimeRangeDto> BusyPeriods);

public sealed record DateTimeRangeDto(
    DateTimeOffset Start,
    DateTimeOffset End);

public sealed record CalendarEntryDto(
    Guid Id,
    Guid VehicleId,
    string Type,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    DateTimeOffset? ExpiresAt,
    Guid? BookingId,
    string? Note);

public sealed record BlockScheduleRequest(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string? Reason = null,
    CalendarEntryType? Type = null);
