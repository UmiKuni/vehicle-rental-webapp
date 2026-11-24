using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Redis;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CarRental.Modules.Availability.Services;

internal sealed class AvailabilityApiService : IAvailabilityApi
{
    private static readonly TimeSpan VehicleLockTtl = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan VehicleLockWait = TimeSpan.FromSeconds(3);
    private const int MaxConcurrencyRetries = 2;

    private readonly AvailabilityDbContext _dbContext;
    private readonly IVehicleCalendarRepository _repository;
    private readonly IFleetApi _fleetApi;
    private readonly TimeProvider _clock;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly ILogger<AvailabilityApiService> _logger;

    public AvailabilityApiService(
        AvailabilityDbContext dbContext,
        IVehicleCalendarRepository repository,
        IFleetApi fleetApi,
        TimeProvider clock,
        IDistributedLockProvider? lockProvider = null,
        ILogger<AvailabilityApiService>? logger = null)
    {
        _dbContext = dbContext;
        _repository = repository;
        _fleetApi = fleetApi;
        _clock = clock;
        _lockProvider = lockProvider ?? new InMemoryDistributedLockProvider();
        _logger = logger ?? NullLogger<AvailabilityApiService>.Instance;
    }

    public async Task<Result<bool>> CheckAvailabilityAsync(
        Guid vehicleId,
        DateTimeRange range,
        CancellationToken ct = default)
    {
        var validation = ValidateVehicleAndRange(vehicleId, range);
        if (validation.IsFailure)
            return validation.Error;

        var hasOverlap = await _repository.HasActiveOverlapAsync(vehicleId, range, _clock.GetUtcNow(), ct);
        return !hasOverlap;
    }

    public async Task<Result<AvailabilityCheckResultDto>> CheckVehicleAvailabilityAsync(
        Guid vehicleId,
        DateTimeRange range,
        CancellationToken ct = default)
    {
        var validation = ValidateVehicleAndRange(vehicleId, range);
        if (validation.IsFailure)
            return validation.Error;

        var overlapping = await _repository.GetActiveOverlappingEntriesAsync(vehicleId, range, _clock.GetUtcNow(), ct);
        var busyPeriods = overlapping
            .Select(e => new DateTimeRangeDto(e.TimeRange.Start, e.TimeRange.End))
            .ToList();

        var isAvailable = busyPeriods.Count == 0;
        return new AvailabilityCheckResultDto(
            vehicleId,
            range.Start,
            range.End,
            isAvailable,
            isAvailable ? null : "Vehicle is unavailable during the requested time range.",
            busyPeriods);
    }

    public async Task<Result<IReadOnlyList<CalendarEntryDto>>> GetCalendarAsync(
        Guid vehicleId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        if (vehicleId == Guid.Empty)
            return AvailabilityErrors.InvalidVehicle;

        if (from.HasValue && to.HasValue && to.Value <= from.Value)
            return AvailabilityErrors.InvalidRange;

        var entries = await _repository.GetActiveEntriesAsync(vehicleId, from, to, _clock.GetUtcNow(), ct);
        return Result.Success<IReadOnlyList<CalendarEntryDto>>(
            entries.Select(e => ToEntryDto(e)).ToList());
    }

    public async Task<Result> PlaceHoldAsync(
        Guid vehicleId,
        Guid bookingId,
        DateTimeRange range,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        var validation = ValidateVehicleAndBooking(vehicleId, bookingId);
        if (validation.IsFailure)
            return validation.Error;

        if (range is null)
            return AvailabilityErrors.InvalidRange;

        await using var vehicleLock = await _lockProvider.TryAcquireAsync(
            GetVehicleLockKey(vehicleId),
            VehicleLockTtl,
            VehicleLockWait,
            ct);

        if (vehicleLock is null)
            return AvailabilityErrors.SlotUnavailable;

        for (var attempt = 0; attempt <= MaxConcurrencyRetries; attempt++)
        {
            var now = _clock.GetUtcNow();
            await _repository.DeleteExpiredHoldsAsync(vehicleId, now, ct);

            var calendar = await GetOrCreateCalendarAsync(vehicleId, now, ct);
            var result = calendar.PlaceHold(bookingId, range, ttl, now);
            if (result.IsFailure)
                return result;

            var saveOutcome = await TrySaveChangesWithRetryAsync(
                vehicleId,
                attempt,
                AvailabilityErrors.SlotUnavailable,
                handleUniqueViolation: true,
                ct);

            if (saveOutcome.ShouldRetry)
            {
                _repository.ClearChangeTracker();
                continue;
            }

            return saveOutcome.Result;
        }

        return AvailabilityErrors.SlotUnavailable;
    }

    public async Task<Result> ConfirmHoldAsync(
        Guid vehicleId,
        Guid bookingId,
        CancellationToken ct = default)
    {
        var validation = ValidateVehicleAndBooking(vehicleId, bookingId);
        if (validation.IsFailure)
            return validation.Error;

        await using var vehicleLock = await _lockProvider.TryAcquireAsync(
            GetVehicleLockKey(vehicleId),
            VehicleLockTtl,
            VehicleLockWait,
            ct);

        if (vehicleLock is null)
            return AvailabilityErrors.HoldNotFoundOrExpired;

        for (var attempt = 0; attempt <= MaxConcurrencyRetries; attempt++)
        {
            var calendar = await _repository.GetByVehicleIdAsync(vehicleId, ct);
            if (calendar is null)
                return AvailabilityErrors.HoldNotFoundOrExpired;

            var result = calendar.ConfirmHold(bookingId, _clock.GetUtcNow());
            if (result.IsFailure)
                return result;

            var saveOutcome = await TrySaveChangesWithRetryAsync(
                vehicleId,
                attempt,
                AvailabilityErrors.HoldNotFoundOrExpired,
                handleUniqueViolation: false,
                ct);

            if (saveOutcome.ShouldRetry)
            {
                _repository.ClearChangeTracker();
                continue;
            }

            return saveOutcome.Result;
        }

        return AvailabilityErrors.HoldNotFoundOrExpired;
    }

    public async Task<Result> ReleaseHoldAsync(
        Guid vehicleId,
        Guid bookingId,
        CancellationToken ct = default)
    {
        var validation = ValidateVehicleAndBooking(vehicleId, bookingId);
        if (validation.IsFailure)
            return validation.Error;

        await using var vehicleLock = await _lockProvider.TryAcquireAsync(
            GetVehicleLockKey(vehicleId),
            VehicleLockTtl,
            VehicleLockWait,
            ct);

        if (vehicleLock is null)
            _logger.LogWarning("Timed out acquiring lock for ReleaseHoldAsync on Vehicle {VehicleId}; falling back to OCC retries.", vehicleId);

        for (var attempt = 0; attempt <= MaxConcurrencyRetries; attempt++)
        {
            var calendar = await _repository.GetByVehicleIdAsync(vehicleId, ct);
            if (calendar is null)
                return Result.Success();

            var result = calendar.Release(bookingId, _clock.GetUtcNow());
            if (result.IsFailure)
                return result;

            var saveOutcome = await TrySaveChangesWithRetryAsync(
                vehicleId,
                attempt,
                AvailabilityErrors.SlotUnavailable,
                handleUniqueViolation: false,
                ct);

            if (saveOutcome.ShouldRetry)
            {
                _repository.ClearChangeTracker();
                continue;
            }

            return saveOutcome.Result;
        }

        return AvailabilityErrors.SlotUnavailable;
    }

    public async Task<Result<CalendarEntryDto>> BlockAsync(
        Guid vehicleId,
        Guid ownerId,
        DateTimeRange range,
        string? reason = null,
        CalendarEntryType type = CalendarEntryType.OwnerBlocked,
        CancellationToken ct = default)
    {
        var validation = ValidateVehicleAndRange(vehicleId, range);
        if (validation.IsFailure)
            return validation.Error;

        var authResult = await EnsureVehicleOwnerAsync(vehicleId, ownerId, ct);
        if (authResult.IsFailure)
            return authResult.Error;

        await using var vehicleLock = await _lockProvider.TryAcquireAsync(
            GetVehicleLockKey(vehicleId),
            VehicleLockTtl,
            VehicleLockWait,
            ct);

        if (vehicleLock is null)
            return AvailabilityErrors.SlotUnavailable;

        for (var attempt = 0; attempt <= MaxConcurrencyRetries; attempt++)
        {
            var now = _clock.GetUtcNow();
            await _repository.DeleteExpiredHoldsAsync(vehicleId, now, ct);

            var calendar = await GetOrCreateCalendarAsync(vehicleId, now, ct);
            var blockResult = calendar.Block(type, range, now, reason);
            if (blockResult.IsFailure)
                return blockResult.Error;

            var saveOutcome = await TrySaveChangesWithRetryAsync(
                vehicleId,
                attempt,
                AvailabilityErrors.SlotUnavailable,
                handleUniqueViolation: true,
                ct);

            if (saveOutcome.ShouldRetry)
            {
                _repository.ClearChangeTracker();
                continue;
            }

            return saveOutcome.Result.IsFailure
                ? saveOutcome.Result.Error
                : ToEntryDto(blockResult.Value);
        }

        return AvailabilityErrors.SlotUnavailable;
    }

    public async Task<Result> UnblockAsync(
        Guid blockId,
        Guid ownerId,
        CancellationToken ct = default)
    {
        if (blockId == Guid.Empty)
            return AvailabilityErrors.EntryNotFound;

        if (ownerId == Guid.Empty)
            return AvailabilityErrors.NotCalendarOwner;

        var initialCalendar = await _repository.GetByEntryIdAsync(blockId, ct);
        if (initialCalendar is null)
            return AvailabilityErrors.EntryNotFound;

        var vehicleId = initialCalendar.VehicleId;
        var authResult = await EnsureVehicleOwnerAsync(vehicleId, ownerId, ct);
        if (authResult.IsFailure)
            return authResult.Error;

        await using var vehicleLock = await _lockProvider.TryAcquireAsync(
            GetVehicleLockKey(vehicleId),
            VehicleLockTtl,
            VehicleLockWait,
            ct);

        if (vehicleLock is null)
            _logger.LogWarning("Timed out acquiring lock for UnblockAsync on Vehicle {VehicleId}; falling back to OCC retries.", vehicleId);

        _repository.ClearChangeTracker();

        for (var attempt = 0; attempt <= MaxConcurrencyRetries; attempt++)
        {
            var calendar = await _repository.GetByVehicleIdAsync(vehicleId, ct);
            if (calendar is null)
                return AvailabilityErrors.EntryNotFound;

            var result = calendar.Unblock(blockId, _clock.GetUtcNow());
            if (result.IsFailure)
                return result;

            var saveOutcome = await TrySaveChangesWithRetryAsync(
                vehicleId,
                attempt,
                AvailabilityErrors.SlotUnavailable,
                handleUniqueViolation: false,
                ct);

            if (saveOutcome.ShouldRetry)
            {
                _repository.ClearChangeTracker();
                continue;
            }

            return saveOutcome.Result;
        }

        return AvailabilityErrors.SlotUnavailable;
    }

    private async Task<VehicleCalendar> GetOrCreateCalendarAsync(
        Guid vehicleId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var calendar = await _repository.GetByVehicleIdAsync(vehicleId, ct);
        if (calendar is not null)
            return calendar;

        calendar = VehicleCalendar.Create(vehicleId, now);
        _repository.Add(calendar);
        return calendar;
    }

    private async Task<Result> EnsureVehicleOwnerAsync(
        Guid vehicleId,
        Guid ownerId,
        CancellationToken ct)
    {
        if (ownerId == Guid.Empty)
            return AvailabilityErrors.NotCalendarOwner;

        var vehicleResult = await _fleetApi.GetVehicleSummaryAsync(vehicleId, ct);
        if (vehicleResult.IsFailure)
            return vehicleResult.Error;

        return vehicleResult.Value.OwnerId == ownerId
            ? Result.Success()
            : AvailabilityErrors.NotCalendarOwner;
    }

    private async Task<(bool ShouldRetry, Result Result)> TrySaveChangesWithRetryAsync(
        Guid vehicleId,
        int attempt,
        Error conflictError,
        bool handleUniqueViolation,
        CancellationToken ct)
    {
        try
        {
            await _dbContext.SaveChangesAsync(ct);
            return (false, Result.Success());
        }
        catch (DbUpdateException ex) when (IsExclusionViolation(ex))
        {
            return (false, AvailabilityErrors.SlotUnavailable);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (attempt < MaxConcurrencyRetries)
            {
                _logger.LogDebug(ex, "Optimistic concurrency conflict on Vehicle {VehicleId}; retrying attempt {Attempt}.", vehicleId, attempt + 1);
                return (true, Result.Success());
            }

            return (false, conflictError);
        }
        catch (DbUpdateException ex) when (handleUniqueViolation && IsUniqueViolation(ex))
        {
            if (attempt < MaxConcurrencyRetries)
            {
                _logger.LogDebug(ex, "Unique violation while creating calendar for Vehicle {VehicleId}; retrying attempt {Attempt}.", vehicleId, attempt + 1);
                return (true, Result.Success());
            }

            return (false, conflictError);
        }
    }

    private static string GetVehicleLockKey(Guid vehicleId) =>
        $"lock:availability:vehicle:{vehicleId:N}";

    private static Result ValidateVehicleAndRange(Guid vehicleId, DateTimeRange? range)
    {
        if (vehicleId == Guid.Empty)
            return AvailabilityErrors.InvalidVehicle;

        if (range is null)
            return AvailabilityErrors.InvalidRange;

        return Result.Success();
    }

    private static Result ValidateVehicleAndBooking(Guid vehicleId, Guid bookingId)
    {
        if (vehicleId == Guid.Empty)
            return AvailabilityErrors.InvalidVehicle;

        if (bookingId == Guid.Empty)
            return AvailabilityErrors.InvalidBooking;

        return Result.Success();
    }

    private static CalendarEntryDto ToEntryDto(CalendarEntry entry) =>
        new(
            entry.Id,
            entry.VehicleId,
            entry.Type.ToString(),
            entry.TimeRange.Start,
            entry.TimeRange.End,
            entry.ExpiresAt,
            entry.BookingId,
            entry.Reason);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static bool IsExclusionViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation };
}
