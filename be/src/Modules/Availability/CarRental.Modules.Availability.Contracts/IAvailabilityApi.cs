using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Availability.Contracts.Dtos;
using CarRental.Modules.Availability.Contracts.Enums;

namespace CarRental.Modules.Availability.Contracts;

public interface IAvailabilityApi
{
    Task<Result> PlaceHoldAsync(
        Guid vehicleId,
        Guid bookingId,
        DateTimeRange range,
        TimeSpan ttl,
        CancellationToken ct = default);

    Task<Result> ConfirmHoldAsync(
        Guid vehicleId,
        Guid bookingId,
        CancellationToken ct = default);

    Task<Result> ReleaseHoldAsync(
        Guid vehicleId,
        Guid bookingId,
        CancellationToken ct = default);

    Task<Result<bool>> CheckAvailabilityAsync(
        Guid vehicleId,
        DateTimeRange range,
        CancellationToken ct = default);

    Task<Result<AvailabilityCheckResultDto>> CheckVehicleAvailabilityAsync(
        Guid vehicleId,
        DateTimeRange range,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<CalendarEntryDto>>> GetCalendarAsync(
        Guid vehicleId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default);

    Task<Result<CalendarEntryDto>> BlockAsync(
        Guid vehicleId,
        Guid ownerId,
        DateTimeRange range,
        string? reason = null,
        CalendarEntryType type = CalendarEntryType.OwnerBlocked,
        CancellationToken ct = default);

    Task<Result> UnblockAsync(
        Guid blockId,
        Guid ownerId,
        CancellationToken ct = default);
}
