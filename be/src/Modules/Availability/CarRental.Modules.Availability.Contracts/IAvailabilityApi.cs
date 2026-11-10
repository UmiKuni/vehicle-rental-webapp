using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;

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
}
