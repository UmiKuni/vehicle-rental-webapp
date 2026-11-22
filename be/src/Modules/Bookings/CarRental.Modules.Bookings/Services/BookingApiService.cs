using System.Text.Json;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Availability.Contracts;
using CarRental.Modules.Bookings.Contracts;
using CarRental.Modules.Bookings.Contracts.Dtos;
using CarRental.Modules.Bookings.Contracts.Enums;
using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Bookings.Data;
using CarRental.Modules.Bookings.Data.Repositories;
using CarRental.Modules.Bookings.Entities;
using CarRental.Modules.Bookings.Errors;
using CarRental.Modules.Fleet.Contracts;
using CarRental.Modules.Fleet.Contracts.Dtos;
using CarRental.Modules.Pricing.Contracts;
using CarRental.Modules.Pricing.Contracts.Dtos;
using CarRental.Modules.Users.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CarRental.Modules.Bookings.Services;

internal sealed class BookingApiService : IBookingApi
{
    private static readonly TimeSpan PendingHoldDuration = TimeSpan.FromHours(12);

    private readonly BookingDbContext _dbContext;
    private readonly IBookingRepository _repository;
    private readonly IUsersApi _usersApi;
    private readonly IFleetApi _fleetApi;
    private readonly IPricingApi _pricingApi;
    private readonly IAvailabilityApi _availabilityApi;
    private readonly IEventOutbox _eventOutbox;
    private readonly TimeProvider _clock;
    private readonly ILogger<BookingApiService> _logger;

    public BookingApiService(
        BookingDbContext dbContext,
        IBookingRepository repository,
        IUsersApi usersApi,
        IFleetApi fleetApi,
        IPricingApi pricingApi,
        IAvailabilityApi availabilityApi,
        IEventOutbox eventOutbox,
        TimeProvider clock,
        ILogger<BookingApiService>? logger = null)
    {
        _dbContext = dbContext;
        _repository = repository;
        _usersApi = usersApi;
        _fleetApi = fleetApi;
        _pricingApi = pricingApi;
        _availabilityApi = availabilityApi;
        _eventOutbox = eventOutbox;
        _clock = clock;
        _logger = logger ?? NullLogger<BookingApiService>.Instance;
    }

    public async Task<Result<BookingDetailDto>> CreateAsync(
        Guid customerId,
        CreateBookingRequest request,
        CancellationToken ct = default)
    {
        if (customerId == Guid.Empty)
            return BookingsErrors.InvalidCustomer;

        if (request is null || request.VehicleId == Guid.Empty)
            return BookingsErrors.InvalidVehicle;

        var periodResult = DateTimeRange.Create(request.StartTime, request.EndTime);
        if (periodResult.IsFailure)
            return BookingsErrors.InvalidDuration;

        var period = periodResult.Value;

        var prereqResult = await FetchCreatePrerequisitesAsync(customerId, request.VehicleId, period, ct);
        if (prereqResult.IsFailure)
            return prereqResult.Error;

        var (vehicle, quote) = prereqResult.Value;
        var bookingId = BookingId.New();
        var lineItems = BuildLineItems(bookingId, quote);
        var policySnapshot = BuildPolicySnapshot(bookingId, quote);
        var now = _clock.GetUtcNow();

        var bookingResult = Booking.Request(
            customerId,
            vehicle.OwnerId,
            request.VehicleId,
            period,
            request.PickupLocation,
            lineItems,
            policySnapshot,
            now,
            bookingId);

        if (bookingResult.IsFailure)
            return bookingResult.Error;

        var booking = bookingResult.Value;
        var persistResult = await PlaceHoldAndSaveAsync(booking, period, ct);
        if (persistResult.IsFailure)
            return persistResult.Error;

        await _eventOutbox.PublishAsync(
            new BookingRequested(
                Guid.CreateVersion7(),
                now,
                booking.Id.Value,
                booking.CustomerId,
                booking.OwnerId,
                booking.VehicleId,
                period,
                quote.Total,
                quote.DepositAmount),
            ct);

        return await MapToDetailDtoAsync(booking, vehicle, ct);
    }

    public Task<Result<PagedResult<BookingSummaryDto>>> GetMyBookingsAsync(
        Guid customerId,
        string? status = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default) =>
        BuildPagedBookingsAsync(
            status,
            page,
            pageSize,
            (parsedStatus, normalizedPage, normalizedPageSize, token) =>
                _repository.GetPagedByCustomerAsync(customerId, parsedStatus, normalizedPage, normalizedPageSize, token),
            ct);

    public Task<Result<PagedResult<BookingSummaryDto>>> GetOwnerBookingsAsync(
        Guid ownerId,
        string? status = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default) =>
        BuildPagedBookingsAsync(
            status,
            page,
            pageSize,
            (parsedStatus, normalizedPage, normalizedPageSize, token) =>
                _repository.GetPagedByOwnerAsync(ownerId, parsedStatus, normalizedPage, normalizedPageSize, token),
            ct);

    public async Task<Result<BookingDetailDto>> GetByIdAsync(
        Guid bookingId,
        Guid actorId,
        bool isAdmin = false,
        CancellationToken ct = default)
    {
        var bookingResult = await GetBookingOrFailAsync(bookingId, readOnly: true, ct);
        if (bookingResult.IsFailure)
            return bookingResult.Error;

        var booking = bookingResult.Value;
        var authResult = EnsureParticipantOrAdmin(booking, actorId, isAdmin);
        if (authResult.IsFailure)
            return authResult.Error;

        return await MapToDetailDtoAsync(booking, null, ct);
    }

    public async Task<Result<BookingDetailDto>> ApproveAsync(
        Guid bookingId,
        Guid ownerId,
        CancellationToken ct = default)
    {
        var bookingResult = await GetBookingOrFailAsync(bookingId, readOnly: false, ct);
        if (bookingResult.IsFailure)
            return bookingResult.Error;

        var booking = bookingResult.Value;
        var now = _clock.GetUtcNow();
        var approveResult = booking.Approve(ownerId, now);
        if (approveResult.IsFailure)
            return approveResult.Error;

        var saveResult = await SaveBookingChangesAsync(booking.Id.Value, ct);
        if (saveResult.IsFailure)
            return saveResult.Error;

        var payableAmount = SumLineItems(
            booking,
            l => l.Type is BookingLineItemType.Rent or BookingLineItemType.CleaningFee or BookingLineItemType.Deposit);
        var depositPortion = SumLineItems(booking, l => l.Type == BookingLineItemType.Deposit);

        await _eventOutbox.PublishAsync(
            new BookingConfirmed(
                Guid.CreateVersion7(),
                now,
                booking.Id.Value,
                payableAmount,
                depositPortion,
                booking.PaymentDeadline!.Value),
            ct);

        return await MapToDetailDtoAsync(booking, null, ct);
    }

    public async Task<Result<BookingDetailDto>> RejectAsync(
        Guid bookingId,
        Guid ownerId,
        string reason,
        CancellationToken ct = default)
    {
        var bookingResult = await GetBookingOrFailAsync(bookingId, readOnly: false, ct);
        if (bookingResult.IsFailure)
            return bookingResult.Error;

        var booking = bookingResult.Value;
        var now = _clock.GetUtcNow();
        var trimmedReason = reason?.Trim() ?? string.Empty;

        var rejectResult = booking.Reject(ownerId, trimmedReason, now);
        if (rejectResult.IsFailure)
            return rejectResult.Error;

        var saveResult = await SaveBookingChangesAsync(booking.Id.Value, ct);
        if (saveResult.IsFailure)
            return saveResult.Error;

        await _availabilityApi.ReleaseHoldAsync(booking.VehicleId, booking.Id.Value, ct);

        await _eventOutbox.PublishAsync(
            new BookingRejected(
                Guid.CreateVersion7(),
                now,
                booking.Id.Value,
                ownerId,
                trimmedReason),
            ct);

        return await MapToDetailDtoAsync(booking, null, ct);
    }

    public async Task<Result<BookingCancellationResultDto>> CancelAsync(
        Guid bookingId,
        Guid customerId,
        string reason,
        CancellationToken ct = default)
    {
        var bookingResult = await GetBookingOrFailAsync(bookingId, readOnly: false, ct);
        if (bookingResult.IsFailure)
            return bookingResult.Error;

        var booking = bookingResult.Value;
        var totalPaid = booking.Status == BookingStatus.Paid
            ? SumLineItemAmounts(
                booking,
                l => l.Type is BookingLineItemType.Rent or BookingLineItemType.CleaningFee or BookingLineItemType.Deposit)
            : 0m;

        var now = _clock.GetUtcNow();
        var trimmedReason = reason?.Trim() ?? string.Empty;

        var cancelResult = booking.Cancel(customerId, trimmedReason, now);
        if (cancelResult.IsFailure)
            return cancelResult.Error;

        var outcome = cancelResult.Value;

        var saveResult = await SaveBookingChangesAsync(booking.Id.Value, ct);
        if (saveResult.IsFailure)
            return saveResult.Error;

        await _availabilityApi.ReleaseHoldAsync(booking.VehicleId, booking.Id.Value, ct);

        await _eventOutbox.PublishAsync(
            new BookingCancelled(
                Guid.CreateVersion7(),
                now,
                booking.Id.Value,
                booking.CustomerId,
                outcome.PenaltyAmount,
                outcome.RefundableAmount,
                trimmedReason),
            ct);

        return new BookingCancellationResultDto(
            booking.Id.Value,
            booking.Status.ToString(),
            totalPaid,
            outcome.PenaltyAmount.Amount,
            outcome.RefundableAmount.Amount,
            trimmedReason,
            now);
    }

    public async Task<Result<BookingDetailDto>> AdminCancelAsync(
        Guid bookingId,
        Guid adminId,
        string reason,
        string penaltyParty,
        CancellationToken ct = default)
    {
        var bookingResult = await GetBookingOrFailAsync(bookingId, readOnly: false, ct);
        if (bookingResult.IsFailure)
            return bookingResult.Error;

        var booking = bookingResult.Value;
        var now = _clock.GetUtcNow();
        var trimmedReason = reason?.Trim() ?? string.Empty;
        var effectiveReason = string.IsNullOrWhiteSpace(trimmedReason) || string.IsNullOrWhiteSpace(penaltyParty)
            ? trimmedReason
            : $"{trimmedReason} (PenaltyParty: {penaltyParty.Trim()})";

        var cancelResult = booking.AdminCancel(adminId, effectiveReason, now, penaltyParty);
        if (cancelResult.IsFailure)
            return cancelResult.Error;

        var outcome = cancelResult.Value;

        var saveResult = await SaveBookingChangesAsync(booking.Id.Value, ct);
        if (saveResult.IsFailure)
            return saveResult.Error;

        await _availabilityApi.ReleaseHoldAsync(booking.VehicleId, booking.Id.Value, ct);

        await _eventOutbox.PublishAsync(
            new BookingCancelled(
                Guid.CreateVersion7(),
                now,
                booking.Id.Value,
                booking.CustomerId,
                outcome.PenaltyAmount,
                outcome.RefundableAmount,
                effectiveReason),
            ct);

        return await MapToDetailDtoAsync(booking, null, ct);
    }

    public async Task<bool> IsParticipantAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken ct = default)
    {
        var bookingResult = await GetBookingOrFailAsync(bookingId, readOnly: true, ct);
        return bookingResult.IsSuccess
            && (bookingResult.Value.CustomerId == userId || bookingResult.Value.OwnerId == userId);
    }

    public async Task<Result> MarkPaidAsync(
        Guid bookingId,
        Guid paymentId,
        DateTimeOffset paidAt,
        CancellationToken ct = default)
    {
        var bookingResult = await GetBookingOrFailAsync(bookingId, readOnly: false, ct);
        if (bookingResult.IsFailure)
            return bookingResult.Error;

        var booking = bookingResult.Value;
        var markPaidResult = booking.MarkPaid(paymentId, paidAt);
        if (markPaidResult.IsFailure)
            return markPaidResult;

        var saveResult = await SaveBookingChangesAsync(booking.Id.Value, ct);
        if (saveResult.IsFailure)
            return saveResult;

        await _availabilityApi.ConfirmHoldAsync(booking.VehicleId, booking.Id.Value, ct);

        await _eventOutbox.PublishAsync(
            new BookingPaid(
                Guid.CreateVersion7(),
                paidAt,
                booking.Id.Value,
                booking.CustomerId,
                booking.OwnerId,
                booking.VehicleId,
                new DateTimeRange(booking.StartTime, booking.EndTime)),
            ct);

        return Result.Success();
    }

    public async Task<Result<BookingRentalDetailsDto>> GetRentalDetailsAsync(
        Guid bookingId,
        CancellationToken ct = default)
    {
        var bookingResult = await GetBookingOrFailAsync(bookingId, readOnly: true, ct);
        if (bookingResult.IsFailure)
            return bookingResult.Error;

        var booking = bookingResult.Value;
        var (totalRentAmount, depositAmount) = CalculateRentalAndDepositAmounts(booking);

        return new BookingRentalDetailsDto(
            booking.Id.Value,
            booking.CustomerId,
            booking.OwnerId,
            booking.VehicleId,
            booking.Status.ToString(),
            booking.StartTime,
            booking.EndTime,
            totalRentAmount,
            depositAmount);
    }

    public async Task<Result> StartTripAsync(
        Guid bookingId,
        DateTimeOffset startedAt,
        CancellationToken ct = default)
    {
        var bookingResult = await GetBookingOrFailAsync(bookingId, readOnly: false, ct);
        if (bookingResult.IsFailure)
            return bookingResult.Error;

        var startResult = bookingResult.Value.StartTrip(startedAt);
        if (startResult.IsFailure)
            return startResult;

        return await SaveBookingChangesAsync(bookingResult.Value.Id.Value, ct);
    }

    public async Task<Result> CompleteTripAsync(
        Guid bookingId,
        decimal? overKmFee,
        DateTimeOffset completedAt,
        CancellationToken ct = default)
    {
        var bookingResult = await GetBookingOrFailAsync(bookingId, readOnly: false, ct);
        if (bookingResult.IsFailure)
            return bookingResult.Error;

        var booking = bookingResult.Value;
        var completeResult = booking.CompleteTrip(completedAt);
        if (completeResult.IsFailure)
            return completeResult;

        if (overKmFee.HasValue && overKmFee.Value > 0)
        {
            var feeResult = booking.AddOverKmFee(Money.Of((long)Math.Round(overKmFee.Value, MidpointRounding.AwayFromZero)));
            if (feeResult.IsFailure)
                return feeResult;
        }

        var saveResult = await SaveBookingChangesAsync(booking.Id.Value, ct);
        if (saveResult.IsFailure)
            return saveResult;

        await _eventOutbox.PublishAsync(
            new BookingCompleted(
                Guid.CreateVersion7(),
                completedAt,
                booking.Id.Value,
                booking.VehicleId,
                booking.CustomerId,
                booking.OwnerId),
            ct);

        return Result.Success();
    }

    private async Task<Result<(VehicleSummaryDto Vehicle, QuoteDto Quote)>> FetchCreatePrerequisitesAsync(
        Guid customerId,
        Guid vehicleId,
        DateTimeRange period,
        CancellationToken ct)
    {
        var eligibilityResult = await _usersApi.GetBookingEligibilityAsync(customerId, ct);
        if (eligibilityResult.IsFailure)
            return eligibilityResult.Error;

        if (!eligibilityResult.Value.IsEligible)
            return BookingsErrors.CustomerNotEligible(eligibilityResult.Value.IneligibilityReason ?? string.Empty);

        var vehicleResult = await _fleetApi.GetVehicleSummaryAsync(vehicleId, ct);
        if (vehicleResult.IsFailure)
            return vehicleResult.Error;

        var vehicle = vehicleResult.Value;
        if (!string.Equals(vehicle.Status, "Published", StringComparison.OrdinalIgnoreCase))
            return BookingsErrors.VehicleNotPublished;

        var quoteResult = await _pricingApi.GetQuoteAsync(vehicleId, period, ct);
        if (quoteResult.IsFailure)
            return quoteResult.Error;

        return (vehicle, quoteResult.Value);
    }

    private static List<BookingLineItem> BuildLineItems(BookingId bookingId, QuoteDto quote)
    {
        var cleaningFeeAmount = quote.Lines
            .Where(l => string.Equals(l.Type, nameof(BookingLineItemType.CleaningFee), StringComparison.OrdinalIgnoreCase))
            .Sum(l => l.Amount.Amount);

        var rentAmount = Math.Max(0L, quote.Total.Amount - cleaningFeeAmount);
        var lineItems = new List<BookingLineItem>
        {
            new(Guid.CreateVersion7(), bookingId, BookingLineItemType.Rent, Money.Of(rentAmount, quote.Total.Currency))
        };

        if (cleaningFeeAmount > 0)
        {
            lineItems.Add(new BookingLineItem(
                Guid.CreateVersion7(),
                bookingId,
                BookingLineItemType.CleaningFee,
                Money.Of(cleaningFeeAmount, quote.Total.Currency)));
        }

        var depositMoney = quote.DepositAmount.Amount > 0
            ? quote.DepositAmount
            : Money.Zero(quote.DepositAmount.Currency);
        lineItems.Add(new BookingLineItem(
            Guid.CreateVersion7(),
            bookingId,
            BookingLineItemType.Deposit,
            depositMoney));

        return lineItems;
    }

    private static CancellationPolicySnapshot BuildPolicySnapshot(BookingId bookingId, QuoteDto quote)
    {
        var policyJson = quote.Policy?.Tiers is { Count: > 0 }
            ? JsonSerializer.Serialize(quote.Policy.Tiers.Select(t => new
            {
                t.HoursBeforeStart,
                PenaltyPercent = Math.Clamp(100m - t.RefundPercent, 0m, 100m)
            }))
            : "[]";

        return new CancellationPolicySnapshot(bookingId, policyJson);
    }

    private async Task<Result> PlaceHoldAndSaveAsync(
        Booking booking,
        DateTimeRange period,
        CancellationToken ct)
    {
        var holdResult = await _availabilityApi.PlaceHoldAsync(
            booking.VehicleId,
            booking.Id.Value,
            period,
            PendingHoldDuration,
            ct);

        if (holdResult.IsFailure)
            return holdResult.Error;

        _repository.Add(booking);
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch
        {
            try
            {
                await _availabilityApi.ReleaseHoldAsync(booking.VehicleId, booking.Id.Value, CancellationToken.None);
            }
            catch (Exception releaseEx)
            {
                _logger.LogError(
                    releaseEx,
                    "Failed to release availability hold for Vehicle {VehicleId}, Booking {BookingId} after booking save failure.",
                    booking.VehicleId,
                    booking.Id.Value);
            }

            throw;
        }

        return Result.Success();
    }

    private async Task<Result> SaveBookingChangesAsync(Guid bookingId, CancellationToken ct)
    {
        try
        {
            await _dbContext.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Optimistic concurrency conflict while updating Booking {BookingId}.", bookingId);
            return BookingsErrors.ConcurrentModification;
        }
    }

    private async Task<Result<PagedResult<BookingSummaryDto>>> BuildPagedBookingsAsync(
        string? status,
        int page,
        int pageSize,
        Func<BookingStatus?, int, int, CancellationToken, Task<(IReadOnlyList<Booking> Items, int TotalCount)>> fetchPage,
        CancellationToken ct)
    {
        if (!TryParseStatus(status, out var parsedStatus))
            return BookingsErrors.InvalidStatusFilter;

        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await fetchPage(parsedStatus, normalizedPage, normalizedPageSize, ct);
        var summaries = await MapToSummaryDtosAsync(items, ct);

        return new PagedResult<BookingSummaryDto>(
            summaries,
            totalCount,
            normalizedPage,
            normalizedPageSize);
    }

    private async Task<Result<Booking>> GetBookingOrFailAsync(
        Guid bookingId,
        bool readOnly,
        CancellationToken ct)
    {
        if (bookingId == Guid.Empty)
            return BookingsErrors.NotFound;

        var id = BookingId.From(bookingId);
        var booking = readOnly
            ? await _repository.GetByIdReadOnlyAsync(id, ct)
            : await _repository.GetByIdAsync(id, ct);

        return booking is not null ? booking : BookingsErrors.NotFound;
    }

    private static Result EnsureParticipantOrAdmin(Booking booking, Guid actorId, bool isAdmin) =>
        isAdmin || booking.CustomerId == actorId || booking.OwnerId == actorId
            ? Result.Success()
            : BookingsErrors.NotParticipant;

    private async Task<IReadOnlyList<BookingSummaryDto>> MapToSummaryDtosAsync(
        IReadOnlyList<Booking> bookings,
        CancellationToken ct)
    {
        var vehicleCache = new Dictionary<Guid, VehicleSummaryDto?>();
        var summaries = new List<BookingSummaryDto>(bookings.Count);

        foreach (var booking in bookings)
        {
            if (!vehicleCache.TryGetValue(booking.VehicleId, out var vehicle))
            {
                var vehicleResult = await _fleetApi.GetVehicleSummaryAsync(booking.VehicleId, ct);
                vehicle = vehicleResult.IsSuccess ? vehicleResult.Value : null;
                vehicleCache[booking.VehicleId] = vehicle;
            }

            var (totalRentAmount, _) = CalculateRentalAndDepositAmounts(booking);

            summaries.Add(new BookingSummaryDto(
                booking.Id.Value,
                booking.VehicleId,
                FormatVehicleModel(vehicle),
                vehicle?.PlateNumber ?? string.Empty,
                booking.StartTime,
                booking.EndTime,
                booking.Status.ToString(),
                totalRentAmount,
                booking.CreatedAt));
        }

        return summaries;
    }

    private async Task<BookingDetailDto> MapToDetailDtoAsync(
        Booking booking,
        VehicleSummaryDto? knownVehicle,
        CancellationToken ct)
    {
        var vehicle = knownVehicle;
        if (vehicle is null)
        {
            var vehicleResult = await _fleetApi.GetVehicleSummaryAsync(booking.VehicleId, ct);
            if (vehicleResult.IsSuccess)
                vehicle = vehicleResult.Value;
        }

        var contactResult = await _usersApi.GetContactAsync(booking.CustomerId, ct);
        var customerName = contactResult.IsSuccess ? contactResult.Value.FullName : string.Empty;
        var customerPhone = contactResult.IsSuccess ? contactResult.Value.Phone : string.Empty;

        var (totalRentAmount, securityDepositAmount) = CalculateRentalAndDepositAmounts(booking);

        DateTimeOffset? holdExpiresAt = booking.Status switch
        {
            BookingStatus.Pending => booking.CreatedAt.Add(PendingHoldDuration),
            BookingStatus.Confirmed => booking.PaymentDeadline,
            _ => null
        };

        var lineItems = booking.LineItems
            .Select(l => new BookingLineItemDto(
                l.Id,
                l.Type.ToString(),
                l.Amount.Amount,
                GetLineItemDescription(l.Type)))
            .ToList();

        var histories = booking.StatusHistory
            .OrderBy(h => h.ChangedAt)
            .Select(h => new BookingStatusHistoryDto(
                h.Status.ToString(),
                h.ChangedAt,
                h.Reason))
            .ToList();

        return new BookingDetailDto(
            booking.Id.Value,
            booking.CustomerId,
            customerName,
            customerPhone,
            booking.OwnerId,
            booking.VehicleId,
            vehicle?.PlateNumber ?? string.Empty,
            FormatVehicleModel(vehicle),
            booking.Status.ToString(),
            booking.StartTime,
            booking.EndTime,
            booking.PickupLocation,
            totalRentAmount,
            securityDepositAmount,
            holdExpiresAt,
            booking.CreatedAt,
            lineItems,
            histories);
    }

    private static string FormatVehicleModel(VehicleSummaryDto? vehicle) =>
        vehicle is null
            ? string.Empty
            : $"{vehicle.BrandName} {vehicle.ModelName}".Trim();

    private static string GetLineItemDescription(BookingLineItemType type) => type switch
    {
        BookingLineItemType.Rent => "Vehicle rental charge",
        BookingLineItemType.Deposit => "Security deposit",
        BookingLineItemType.CleaningFee => "Cleaning fee",
        BookingLineItemType.OverKmFee => "Over-kilometre surcharge",
        _ => type.ToString()
    };

    private static Money SumLineItems(Booking booking, Func<BookingLineItem, bool> predicate) =>
        booking.LineItems
            .Where(predicate)
            .Aggregate(Money.Zero(), (acc, item) => acc.Add(item.Amount));

    private static decimal SumLineItemAmounts(Booking booking, Func<BookingLineItem, bool> predicate) =>
        booking.LineItems
            .Where(predicate)
            .Sum(l => l.Amount.Amount);

    private static (decimal TotalRentAmount, decimal DepositAmount) CalculateRentalAndDepositAmounts(Booking booking) =>
        (
            SumLineItemAmounts(booking, l => l.Type != BookingLineItemType.Deposit),
            SumLineItemAmounts(booking, l => l.Type == BookingLineItemType.Deposit)
        );

    private static bool TryParseStatus(string? status, out BookingStatus? parsedStatus)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            parsedStatus = null;
            return true;
        }

        if (Enum.TryParse<BookingStatus>(status.Trim(), ignoreCase: true, out var value)
            && Enum.IsDefined(value))
        {
            parsedStatus = value;
            return true;
        }

        parsedStatus = null;
        return false;
    }
}
