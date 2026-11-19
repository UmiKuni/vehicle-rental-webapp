using CarRental.BuildingBlocks.Messaging;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Bookings.Contracts;
using CarRental.Modules.Bookings.Contracts.Dtos;
using CarRental.Modules.Handover.Contracts;
using CarRental.Modules.Handover.Contracts.Dtos;
using CarRental.Modules.Handover.Contracts.Enums;
using CarRental.Modules.Handover.Contracts.Events;
using CarRental.Modules.Handover.Data;
using CarRental.Modules.Handover.Data.Repositories;
using CarRental.Modules.Handover.Entities;
using CarRental.Modules.Handover.Errors;
using CarRental.Modules.Pricing.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using HandoverEntity = CarRental.Modules.Handover.Entities.Handover;

namespace CarRental.Modules.Handover.Services;

internal sealed class HandoverApiService : IHandoverApi
{
    private readonly HandoverDbContext _dbContext;
    private readonly IHandoverRepository _repository;
    private readonly IBookingApi _bookingApi;
    private readonly IPricingApi _pricingApi;
    private readonly IEventOutbox _eventOutbox;
    private readonly TimeProvider _clock;

    public HandoverApiService(
        HandoverDbContext dbContext,
        IHandoverRepository repository,
        IBookingApi bookingApi,
        IPricingApi pricingApi,
        IEventOutbox eventOutbox,
        TimeProvider clock)
    {
        _dbContext = dbContext;
        _repository = repository;
        _bookingApi = bookingApi;
        _pricingApi = pricingApi;
        _eventOutbox = eventOutbox;
        _clock = clock;
    }

    public async Task<Result<HandoverDetailDto>> CreateAsync(
        Guid actorId,
        CreateHandoverRequest request,
        CancellationToken ct = default)
    {
        if (!TryParseEnum(request.Type, out HandoverType type))
            return HandoverErrors.InvalidType;

        var authResult = await EnsureParticipantAsync(request.BookingId, actorId, isAdmin: false, ct);
        if (authResult.IsFailure)
            return authResult.Error;

        var detailsResult = await _bookingApi.GetRentalDetailsAsync(request.BookingId, ct);
        if (detailsResult.IsFailure)
            return detailsResult.Error;

        var details = detailsResult.Value;
        if (type == HandoverType.Pickup && !string.Equals(details.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            return HandoverErrors.BookingNotReadyForPickup;

        if (type == HandoverType.Return && !string.Equals(details.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
            return HandoverErrors.BookingNotInProgress;

        var existing = await _repository.GetByBookingAndTypeAsync(request.BookingId, type, ct);
        if (existing is not null)
            return HandoverErrors.AlreadyExistsForType;

        var context = await GetOrCreateContextAsync(details, ct);

        var startResult = HandoverEntity.Start(context, type, actorId, _clock.GetUtcNow());
        if (startResult.IsFailure)
            return startResult.Error;

        var handover = startResult.Value;
        _repository.Add(handover);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
            return ToDetailDto(handover);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return HandoverErrors.AlreadyExistsForType;
        }
    }

    public async Task<Result<IReadOnlyList<HandoverSummaryDto>>> GetByBookingAsync(
        Guid bookingId,
        Guid actorId,
        bool isAdmin = false,
        CancellationToken ct = default)
    {
        var authResult = await EnsureParticipantAsync(bookingId, actorId, isAdmin, ct);
        if (authResult.IsFailure)
            return authResult.Error;

        var handovers = await _repository.GetByBookingIdAsync(bookingId, ct);
        return Result.Success<IReadOnlyList<HandoverSummaryDto>>(
            handovers.Select(ToSummaryDto).ToList());
    }

    public async Task<Result<HandoverDetailDto>> GetByIdAsync(
        Guid handoverId,
        Guid actorId,
        bool isAdmin = false,
        CancellationToken ct = default)
    {
        var handoverResult = await GetHandoverOrFailAsync(handoverId, ct);
        if (handoverResult.IsFailure)
            return handoverResult.Error;

        var handover = handoverResult.Value;
        var authResult = await EnsureParticipantAsync(handover.BookingId, actorId, isAdmin, ct);
        if (authResult.IsFailure)
            return authResult.Error;

        return ToDetailDto(handover);
    }

    public async Task<Result<VehicleConditionDto>> UpdateConditionAsync(
        Guid handoverId,
        Guid actorId,
        UpdateVehicleConditionRequest request,
        CancellationToken ct = default)
    {
        var handoverResult = await GetHandoverOrFailAsync(handoverId, ct);
        if (handoverResult.IsFailure)
            return handoverResult.Error;

        var handover = handoverResult.Value;
        var authResult = await EnsureParticipantAsync(handover.BookingId, actorId, isAdmin: false, ct);
        if (authResult.IsFailure)
            return authResult.Error;

        var result = handover.SetCondition(request.Odo, request.FuelLevel, request.DamageNote);
        if (result.IsFailure)
            return result.Error;

        await _dbContext.SaveChangesAsync(ct);
        return ToConditionDto(handover.Condition!);
    }

    public async Task<Result<HandoverPhotoDto>> AddPhotoAsync(
        Guid handoverId,
        Guid actorId,
        UploadHandoverPhotoRequest request,
        CancellationToken ct = default)
    {
        if (!TryParseEnum(request.Corner, out PhotoCorner corner))
            return HandoverErrors.InvalidCorner;

        var handoverResult = await GetHandoverOrFailAsync(handoverId, ct);
        if (handoverResult.IsFailure)
            return handoverResult.Error;

        var handover = handoverResult.Value;
        var authResult = await EnsureParticipantAsync(handover.BookingId, actorId, isAdmin: false, ct);
        if (authResult.IsFailure)
            return authResult.Error;

        var result = handover.AddPhoto(corner, request.StorageKey, request.FileHash, request.ClientCapturedAt);
        if (result.IsFailure)
            return result.Error;

        await _dbContext.SaveChangesAsync(ct);

        var photo = handover.Photos.Single(p => p.Corner == corner);
        return ToPhotoDto(photo);
    }

    public async Task<Result<HandoverDetailDto>> ConfirmAsync(
        Guid handoverId,
        Guid actorId,
        ConfirmHandoverRequest request,
        CancellationToken ct = default)
    {
        if (!TryParseConfirmationRole(request.ConfirmedByRole, out var isCustomerRole))
            return HandoverErrors.InvalidRole;

        var handoverResult = await GetHandoverOrFailAsync(handoverId, ct);
        if (handoverResult.IsFailure)
            return handoverResult.Error;

        var handover = handoverResult.Value;
        var detailsResult = await _bookingApi.GetRentalDetailsAsync(handover.BookingId, ct);
        if (detailsResult.IsFailure)
            return detailsResult.Error;

        var details = detailsResult.Value;
        if (actorId != details.CustomerId && actorId != details.OwnerId)
            return HandoverErrors.NotParticipant;

        var expectedActorId = isCustomerRole ? details.CustomerId : details.OwnerId;
        if (actorId != expectedActorId)
            return HandoverErrors.RoleMismatch;

        int? pickupOdometer = null;
        if (handover.Type == HandoverType.Return)
        {
            var pickup = await _repository.GetByBookingAndTypeAsync(handover.BookingId, HandoverType.Pickup, ct);
            pickupOdometer = pickup?.Condition?.Odometer;
        }

        var now = _clock.GetUtcNow();
        var confirmResult = handover.Confirm(actorId, details.CustomerId, details.OwnerId, now, pickupOdometer);
        if (confirmResult.IsFailure)
            return confirmResult.Error;

        await _dbContext.SaveChangesAsync(ct);

        if (handover.Status == HandoverStatus.Confirmed)
        {
            var sideEffectsResult = await PublishConfirmedSideEffectsAsync(handover, details, pickupOdometer, now, ct);
            if (sideEffectsResult.IsFailure)
                return sideEffectsResult.Error;
        }

        return ToDetailDto(handover);
    }

    private async Task<Result<HandoverEntity>> GetHandoverOrFailAsync(Guid handoverId, CancellationToken ct)
    {
        if (handoverId == Guid.Empty)
            return HandoverErrors.NotFound;

        var handover = await _repository.GetByIdAsync(HandoverId.From(handoverId), ct);
        return handover is not null ? handover : HandoverErrors.NotFound;
    }

    private async Task<Result> EnsureParticipantAsync(
        Guid bookingId,
        Guid actorId,
        bool isAdmin,
        CancellationToken ct)
    {
        if (isAdmin || await _bookingApi.IsParticipantAsync(bookingId, actorId, ct))
            return Result.Success();

        return HandoverErrors.NotParticipant;
    }

    private async Task<HandoverContext> GetOrCreateContextAsync(
        BookingRentalDetailsDto details,
        CancellationToken ct)
    {
        var context = await _repository.GetContextAsync(details.BookingId, ct);
        if (context is not null)
            return context;

        context = new HandoverContext(
            details.BookingId,
            details.VehicleId,
            details.CustomerId,
            details.OwnerId,
            details.StartTime,
            details.EndTime);
        _repository.AddContext(context);
        return context;
    }

    private async Task<Result> PublishConfirmedSideEffectsAsync(
        HandoverEntity handover,
        BookingRentalDetailsDto details,
        int? pickupOdometer,
        DateTimeOffset now,
        CancellationToken ct)
    {
        int? distanceKm = handover.Type == HandoverType.Return && pickupOdometer.HasValue
            ? handover.Condition!.Odometer - pickupOdometer.Value
            : null;

        if (handover.Type == HandoverType.Pickup)
        {
            var startResult = await _bookingApi.StartTripAsync(handover.BookingId, now, ct);
            if (startResult.IsFailure)
                return startResult.Error;
        }
        else if (handover.Type == HandoverType.Return)
        {
            var overKmFee = await CalculateOverKmFeeAsync(
                details.VehicleId,
                details.StartTime,
                details.EndTime,
                distanceKm,
                ct);

            var completeResult = await _bookingApi.CompleteTripAsync(
                handover.BookingId,
                overKmFee > 0 ? overKmFee : null,
                now,
                ct);
            if (completeResult.IsFailure)
                return completeResult.Error;
        }

        await _eventOutbox.PublishAsync(
            new HandoverConfirmed(
                Guid.CreateVersion7(),
                now,
                handover.BookingId,
                handover.Type.ToString(),
                handover.Condition!.Odometer,
                handover.Condition.FuelLevel,
                now,
                distanceKm),
            ct);

        return Result.Success();
    }

    private async Task<decimal?> CalculateOverKmFeeAsync(
        Guid vehicleId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int? distanceKm,
        CancellationToken ct)
    {
        var pricingResult = await _pricingApi.GetPricingAsync(vehicleId, ct);
        if (pricingResult.IsFailure)
            return null;

        var pricing = pricingResult.Value;
        var overKmRule = pricing.FeeRules.FirstOrDefault(
            r => string.Equals(r.Type, "OverKm", StringComparison.OrdinalIgnoreCase));
        if (overKmRule is null)
            return null;

        var days = Math.Max(1, (int)Math.Ceiling((endTime - startTime).TotalDays));
        var allowedKm = pricing.KmLimit * days;
        var overKm = Math.Max(0, (distanceKm ?? 0) - allowedKm);
        return overKm * overKmRule.Amount.Amount;
    }

    private static bool TryParseEnum<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(value) || int.TryParse(value, out _))
            return false;

        return Enum.TryParse(value.Trim(), ignoreCase: true, out parsed) && Enum.IsDefined(parsed);
    }

    private static bool TryParseConfirmationRole(string? role, out bool isCustomer)
    {
        isCustomer = false;
        var trimmed = role?.Trim();
        if (string.Equals(trimmed, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            isCustomer = true;
            return true;
        }

        return string.Equals(trimmed, "Owner", StringComparison.OrdinalIgnoreCase);
    }

    private static HandoverDetailDto ToDetailDto(HandoverEntity handover) =>
        new(
            handover.Id.Value,
            handover.BookingId,
            handover.Type.ToString(),
            handover.Status.ToString(),
            handover.ServerReceivedAt,
            handover.Condition is null ? null : ToConditionDto(handover.Condition),
            handover.Photos
                .OrderBy(p => p.Corner)
                .Select(ToPhotoDto)
                .ToList(),
            handover.ConfirmedByOwnerAt.HasValue,
            handover.ConfirmedByCustomerAt.HasValue,
            handover.Status == HandoverStatus.Confirmed
                ? (handover.ConfirmedByCustomerAt >= handover.ConfirmedByOwnerAt
                    ? handover.ConfirmedByCustomerAt
                    : handover.ConfirmedByOwnerAt)
                : null);

    private static HandoverSummaryDto ToSummaryDto(HandoverEntity handover) =>
        new(
            handover.Id.Value,
            handover.BookingId,
            handover.Type.ToString(),
            handover.Status.ToString(),
            handover.ServerReceivedAt);

    private static VehicleConditionDto ToConditionDto(VehicleCondition condition) =>
        new(
            condition.HandoverId.Value,
            condition.Odometer,
            condition.FuelLevel,
            condition.DamageNotes);

    private static HandoverPhotoDto ToPhotoDto(HandoverPhoto photo) =>
        new(
            photo.Id,
            photo.HandoverId.Value,
            photo.Corner.ToString(),
            photo.StorageKey,
            photo.FileHash,
            $"https://storage.carrental.vn/handovers/{photo.StorageKey}",
            photo.ClientCapturedAt,
            photo.ClientCapturedAt);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
