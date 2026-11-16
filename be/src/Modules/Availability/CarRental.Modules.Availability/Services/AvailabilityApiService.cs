using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Availability.Contracts;
using CarRental.Modules.Availability.Contracts.Dtos;
using CarRental.Modules.Availability.Contracts.Enums;
using CarRental.Modules.Availability.Data;
using CarRental.Modules.Availability.Data.Repositories;
using CarRental.Modules.Availability.Entities;
using CarRental.Modules.Availability.Errors;
using CarRental.Modules.Fleet.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CarRental.Modules.Availability.Services;

public sealed class AvailabilityApiService : IAvailabilityApi
{
    private readonly AvailabilityDbContext _dbContext;
    private readonly IVehicleCalendarRepository _repository;
    private readonly IFleetApi _fleetApi;
    private readonly TimeProvider _clock;

    public AvailabilityApiService(
        AvailabilityDbContext dbContext,
        IVehicleCalendarRepository repository,
        IFleetApi fleetApi,
        TimeProvider clock)
    {
        _dbContext = dbContext;
        _repository = repository;
        _fleetApi = fleetApi;
        _clock = clock;
    }

    public async Task<Result<bool>> CheckAvailabilityAsync(
        Guid vehicleId,
        DateTimeRange range,
        CancellationToken ct = default)
    {
        if (vehicleId == Guid.Empty)
            return Result.Failure<bool>(AvailabilityErrors.InvalidVehicle);

        if (range is null)
            return Result.Failure<bool>(AvailabilityErrors.InvalidRange);

        var hasOverlap = await _repository.HasActiveOverlapAsync(vehicleId, range, _clock.GetUtcNow(), ct);
        return Result.Success(!hasOverlap);
    }

    public async Task<Result<AvailabilityCheckResultDto>> CheckVehicleAvailabilityAsync(
        Guid vehicleId,
        DateTimeRange range,
        CancellationToken ct = default)
    {
        if (vehicleId == Guid.Empty)
            return Result.Failure<AvailabilityCheckResultDto>(AvailabilityErrors.InvalidVehicle);

        if (range is null)
            return Result.Failure<AvailabilityCheckResultDto>(AvailabilityErrors.InvalidRange);

        var overlapping = await _repository.GetActiveOverlappingEntriesAsync(vehicleId, range, _clock.GetUtcNow(), ct);
        var busyPeriods = overlapping
            .Select(e => new DateTimeRangeDto(e.TimeRange.Start, e.TimeRange.End))
            .ToList();

        var isAvailable = busyPeriods.Count == 0;
        return Result.Success(new AvailabilityCheckResultDto(
            vehicleId,
            range.Start,
            range.End,
            isAvailable,
            isAvailable ? null : "Vehicle is unavailable during the requested time range.",
            busyPeriods));
    }

    public async Task<Result<IReadOnlyList<CalendarEntryDto>>> GetCalendarAsync(
        Guid vehicleId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        if (vehicleId == Guid.Empty)
            return Result.Failure<IReadOnlyList<CalendarEntryDto>>(AvailabilityErrors.InvalidVehicle);

        if (from.HasValue && to.HasValue && to.Value <= from.Value)
            return Result.Failure<IReadOnlyList<CalendarEntryDto>>(AvailabilityErrors.InvalidRange);

        var entries = await _repository.GetActiveEntriesAsync(vehicleId, from, to, _clock.GetUtcNow(), ct);
        return Result.Success<IReadOnlyList<CalendarEntryDto>>(
            entries.Select(e => ToEntryDto(e, null)).ToList());
    }

    public async Task<Result> PlaceHoldAsync(
        Guid vehicleId,
        Guid bookingId,
        DateTimeRange range,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        if (vehicleId == Guid.Empty)
            return Result.Failure(AvailabilityErrors.InvalidVehicle);

        if (bookingId == Guid.Empty)
            return Result.Failure(AvailabilityErrors.InvalidBooking);

        if (range is null)
            return Result.Failure(AvailabilityErrors.InvalidRange);

        var now = _clock.GetUtcNow();
        var calendar = await _repository.GetByVehicleIdAsync(vehicleId, ct);
        if (calendar is null)
        {
            calendar = VehicleCalendar.Create(vehicleId, now);
            _repository.Add(calendar);
        }

        var result = calendar.PlaceHold(bookingId, range, ttl, now);
        if (result.IsFailure)
            return result;

        try
        {
            await _dbContext.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(AvailabilityErrors.SlotUnavailable);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Result.Failure(AvailabilityErrors.SlotUnavailable);
        }
    }

    public async Task<Result> ConfirmHoldAsync(
        Guid vehicleId,
        Guid bookingId,
        CancellationToken ct = default)
    {
        if (vehicleId == Guid.Empty)
            return Result.Failure(AvailabilityErrors.InvalidVehicle);

        if (bookingId == Guid.Empty)
            return Result.Failure(AvailabilityErrors.InvalidBooking);

        var calendar = await _repository.GetByVehicleIdAsync(vehicleId, ct);
        if (calendar is null)
            return Result.Failure(AvailabilityErrors.HoldNotFoundOrExpired);

        var result = calendar.ConfirmHold(bookingId, _clock.GetUtcNow());
        if (result.IsFailure)
            return result;

        try
        {
            await _dbContext.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(AvailabilityErrors.HoldNotFoundOrExpired);
        }
    }

    public async Task<Result> ReleaseHoldAsync(
        Guid vehicleId,
        Guid bookingId,
        CancellationToken ct = default)
    {
        if (vehicleId == Guid.Empty)
            return Result.Failure(AvailabilityErrors.InvalidVehicle);

        if (bookingId == Guid.Empty)
            return Result.Failure(AvailabilityErrors.InvalidBooking);

        var calendar = await _repository.GetByVehicleIdAsync(vehicleId, ct);
        if (calendar is null)
            return Result.Success();

        var result = calendar.Release(bookingId, _clock.GetUtcNow());
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<CalendarEntryDto>> BlockAsync(
        Guid vehicleId,
        Guid ownerId,
        DateTimeRange range,
        string? reason = null,
        CalendarEntryType type = CalendarEntryType.OwnerBlocked,
        CancellationToken ct = default)
    {
        if (vehicleId == Guid.Empty)
            return Result.Failure<CalendarEntryDto>(AvailabilityErrors.InvalidVehicle);

        if (ownerId == Guid.Empty)
            return Result.Failure<CalendarEntryDto>(AvailabilityErrors.NotCalendarOwner);

        if (range is null)
            return Result.Failure<CalendarEntryDto>(AvailabilityErrors.InvalidRange);

        var vehicleResult = await _fleetApi.GetVehicleSummaryAsync(vehicleId, ct);
        if (vehicleResult.IsFailure)
            return Result.Failure<CalendarEntryDto>(vehicleResult.Error);

        if (vehicleResult.Value.OwnerId != ownerId)
            return Result.Failure<CalendarEntryDto>(AvailabilityErrors.NotCalendarOwner);

        var now = _clock.GetUtcNow();
        var calendar = await _repository.GetByVehicleIdAsync(vehicleId, ct);
        if (calendar is null)
        {
            calendar = VehicleCalendar.Create(vehicleId, now);
            _repository.Add(calendar);
        }

        var existingEntryIds = calendar.Entries.Select(e => e.Id).ToHashSet();
        var blockResult = calendar.Block(type, range, now);
        if (blockResult.IsFailure)
            return Result.Failure<CalendarEntryDto>(blockResult.Error);

        var createdEntry = calendar.Entries.Single(e => !existingEntryIds.Contains(e.Id));

        try
        {
            await _dbContext.SaveChangesAsync(ct);
            return Result.Success(ToEntryDto(createdEntry, reason));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<CalendarEntryDto>(AvailabilityErrors.SlotUnavailable);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Result.Failure<CalendarEntryDto>(AvailabilityErrors.SlotUnavailable);
        }
    }

    public async Task<Result> UnblockAsync(
        Guid blockId,
        Guid ownerId,
        CancellationToken ct = default)
    {
        if (blockId == Guid.Empty)
            return Result.Failure(AvailabilityErrors.EntryNotFound);

        if (ownerId == Guid.Empty)
            return Result.Failure(AvailabilityErrors.NotCalendarOwner);

        var calendar = await _repository.GetByEntryIdAsync(blockId, ct);
        if (calendar is null)
            return Result.Failure(AvailabilityErrors.EntryNotFound);

        var vehicleResult = await _fleetApi.GetVehicleSummaryAsync(calendar.VehicleId, ct);
        if (vehicleResult.IsFailure)
            return Result.Failure(vehicleResult.Error);

        if (vehicleResult.Value.OwnerId != ownerId)
            return Result.Failure(AvailabilityErrors.NotCalendarOwner);

        var result = calendar.Unblock(blockId, _clock.GetUtcNow());
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static CalendarEntryDto ToEntryDto(CalendarEntry entry, string? note) =>
        new(
            entry.Id,
            entry.VehicleId,
            entry.Type.ToString(),
            entry.TimeRange.Start,
            entry.TimeRange.End,
            entry.ExpiresAt,
            entry.BookingId,
            note);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
