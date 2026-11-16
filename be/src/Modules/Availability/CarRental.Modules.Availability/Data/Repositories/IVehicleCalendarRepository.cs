using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Availability.Entities;

namespace CarRental.Modules.Availability.Data.Repositories;

public interface IVehicleCalendarRepository
{
    Task<VehicleCalendar?> GetByVehicleIdAsync(Guid vehicleId, CancellationToken ct = default);

    Task<VehicleCalendar?> GetByEntryIdAsync(Guid entryId, CancellationToken ct = default);

    Task<VehicleCalendar?> GetByBookingIdAsync(Guid bookingId, CancellationToken ct = default);

    Task<bool> HasActiveOverlapAsync(
        Guid vehicleId,
        DateTimeRange range,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task<IReadOnlyList<CalendarEntry>> GetActiveOverlappingEntriesAsync(
        Guid vehicleId,
        DateTimeRange range,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task<IReadOnlyList<CalendarEntry>> GetActiveEntriesAsync(
        Guid vehicleId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task<IReadOnlyList<VehicleCalendar>> GetCalendarsWithExpiredHoldsAsync(
        DateTimeOffset now,
        CancellationToken ct = default);

    void Add(VehicleCalendar calendar);
}
