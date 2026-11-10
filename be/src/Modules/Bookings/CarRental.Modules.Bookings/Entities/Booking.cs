using CarRental.Modules.Bookings.Errors;
using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Bookings.Contracts.Enums;

namespace CarRental.Modules.Bookings.Entities;

public class Booking : AggregateRoot<BookingId>
{
    private readonly List<BookingLineItem> _lineItems = new();
    private readonly List<BookingStatusHistory> _statusHistory = new();

    public Guid CustomerId { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public BookingStatus Status { get; private set; }
    public DateTimeOffset StartTime { get; private set; }
    public DateTimeOffset EndTime { get; private set; }
    public string PickupLocation { get; private set; } = string.Empty;
    public DateTimeOffset? PaymentDeadline { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public CancellationPolicySnapshot? PolicySnapshot { get; private set; }

    public IReadOnlyCollection<BookingLineItem> LineItems => _lineItems.AsReadOnly();
    public IReadOnlyCollection<BookingStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    private Booking() { }

    private Booking(
        BookingId id,
        Guid customerId,
        Guid ownerId,
        Guid vehicleId,
        DateTimeRange period,
        string pickupLocation,
        IEnumerable<BookingLineItem> lineItems,
        CancellationPolicySnapshot policy,
        DateTimeOffset now)
    {
        Id = id;
        CustomerId = customerId;
        OwnerId = ownerId;
        VehicleId = vehicleId;
        Status = BookingStatus.Pending;
        StartTime = period.Start;
        EndTime = period.End;
        PickupLocation = pickupLocation.Trim();
        CreatedAt = now;
        UpdatedAt = now;
        PolicySnapshot = policy;

        _lineItems.AddRange(lineItems);
        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), id, BookingStatus.Pending, now, "Booking requested"));
    }

    public static Result<Booking> Request(
        Guid customerId,
        Guid ownerId,
        Guid vehicleId,
        DateTimeRange period,
        string pickupLocation,
        IReadOnlyList<BookingLineItem> lineItems,
        CancellationPolicySnapshot policy,
        DateTimeOffset now)
    {
        if (customerId == Guid.Empty)
            return Result.Failure<Booking>(BookingsErrors.InvalidCustomer);

        if (ownerId == Guid.Empty)
            return Result.Failure<Booking>(BookingsErrors.InvalidOwner);

        if (vehicleId == Guid.Empty)
            return Result.Failure<Booking>(BookingsErrors.InvalidVehicle);

        if (customerId == ownerId)
            return Result.Failure<Booking>(BookingsErrors.CustomerIsOwner);

        if (period == null)
            return Result.Failure<Booking>(BookingsErrors.InvalidPeriod);

        if (period.Start <= now)
            return Result.Failure<Booking>(BookingsErrors.StartInPast);

        if (period.End <= period.Start)
            return Result.Failure<Booking>(BookingsErrors.InvalidDuration);

        if (string.IsNullOrWhiteSpace(pickupLocation))
            return Result.Failure<Booking>(BookingsErrors.InvalidLocation);

        if (lineItems == null || lineItems.Count == 0)
            return Result.Failure<Booking>(BookingsErrors.MissingLineItems);

        if (!lineItems.Any(l => l.Type == BookingLineItemType.Rent))
            return Result.Failure<Booking>(BookingsErrors.MissingRentItem);

        if (!lineItems.Any(l => l.Type == BookingLineItemType.Deposit))
            return Result.Failure<Booking>(BookingsErrors.MissingDepositItem);

        if (policy == null)
            return Result.Failure<Booking>(BookingsErrors.MissingPolicy);

        var bookingId = BookingId.New();
        return Result.Success(new Booking(
            bookingId,
            customerId,
            ownerId,
            vehicleId,
            period,
            pickupLocation,
            lineItems,
            policy,
            now));
    }

    public Result Approve(Guid ownerId, DateTimeOffset now)
    {
        if (ownerId != OwnerId)
            return Result.Failure(BookingsErrors.Unauthorized5E2B0A);

        if (Status != BookingStatus.Pending)
            return Result.Failure(Error.Conflict("booking.invalid_state", $"Only pending bookings can be approved. Current status: {Status}."));

        Status = BookingStatus.Confirmed;
        PaymentDeadline = now.AddHours(2);
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id, BookingStatus.Confirmed, now, "Approved by owner"));
        return Result.Success();
    }

    public Result Reject(Guid ownerId, string reason, DateTimeOffset now)
    {
        if (ownerId != OwnerId)
            return Result.Failure(BookingsErrors.UnauthorizedBDDFB3);

        if (Status != BookingStatus.Pending)
            return Result.Failure(Error.Conflict("booking.invalid_state", $"Only pending bookings can be rejected. Current status: {Status}."));

        Status = BookingStatus.Rejected;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id, BookingStatus.Rejected, now, reason));
        return Result.Success();
    }

    public Result MarkPaid(Guid paymentId, DateTimeOffset now)
    {
        if (Status != BookingStatus.Confirmed)
            return Result.Failure(Error.Conflict("booking.invalid_state", $"Only confirmed bookings can be marked paid. Current status: {Status}."));

        Status = BookingStatus.Paid;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id, BookingStatus.Paid, now, $"Payment marked paid (Payment ID: {paymentId})"));
        return Result.Success();
    }

    public Result<CancellationOutcome> Cancel(Guid actorId, string reason, DateTimeOffset now)
    {
        if (actorId != CustomerId)
            return Result.Failure<CancellationOutcome>(BookingsErrors.Unauthorized6779FC);

        if (IsTerminalState(Status))
            return Result.Failure<CancellationOutcome>(Error.Conflict("booking.terminal_state", $"Cannot cancel a booking in terminal state {Status}."));

        if (Status == BookingStatus.InProgress)
            return Result.Failure<CancellationOutcome>(BookingsErrors.TripInProgress);

        Money penalty = Money.Zero();
        Money refundable = Money.Zero();

        if (Status == BookingStatus.Paid)
        {
            var rentTotal = _lineItems
                .Where(l => l.Type == BookingLineItemType.Rent)
                .Aggregate(Money.Zero(), (acc, item) => acc.Add(item.Amount));

            var depositTotal = _lineItems
                .Where(l => l.Type == BookingLineItemType.Deposit)
                .Aggregate(Money.Zero(), (acc, item) => acc.Add(item.Amount));

            var timeUntilStart = StartTime > now ? StartTime - now : TimeSpan.Zero;

            if (PolicySnapshot != null)
            {
                var outcome = PolicySnapshot.Calculate(rentTotal, depositTotal, timeUntilStart);
                penalty = outcome.PenaltyAmount;
                refundable = outcome.RefundableAmount;
            }
            else
            {
                refundable = depositTotal;
            }
        }

        Status = BookingStatus.Cancelled;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id, BookingStatus.Cancelled, now, reason));
        return Result.Success(new CancellationOutcome(penalty, refundable));
    }

    public Result AdminCancel(Guid adminId, string reason, DateTimeOffset now)
    {
        if (adminId == Guid.Empty)
            return Result.Failure(BookingsErrors.InvalidAdmin);

        if (IsTerminalState(Status))
            return Result.Failure(Error.Conflict("booking.terminal_state", $"Cannot cancel a booking in terminal state {Status}."));

        Status = BookingStatus.Cancelled;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id, BookingStatus.Cancelled, now, $"Admin cancel: {reason}"));
        return Result.Success();
    }

    public Result Expire(string reason, DateTimeOffset now)
    {
        if (Status != BookingStatus.Pending && Status != BookingStatus.Confirmed)
            return Result.Failure(Error.Conflict("booking.invalid_state", $"Only pending or confirmed bookings can expire. Current status: {Status}."));

        Status = BookingStatus.Expired;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id, BookingStatus.Expired, now, reason));
        return Result.Success();
    }

    public Result StartTrip(DateTimeOffset now)
    {
        if (Status != BookingStatus.Paid)
            return Result.Failure(Error.Conflict("booking.invalid_state", $"Trip can only be started from Paid status. Current status: {Status}."));

        Status = BookingStatus.InProgress;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id, BookingStatus.InProgress, now, "Trip started"));
        return Result.Success();
    }

    public Result CompleteTrip(DateTimeOffset now)
    {
        if (Status != BookingStatus.InProgress)
            return Result.Failure(Error.Conflict("booking.invalid_state", $"Trip can only be completed from InProgress status. Current status: {Status}."));

        Status = BookingStatus.Completed;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id, BookingStatus.Completed, now, "Trip completed"));
        return Result.Success();
    }

    public Result AddOverKmFee(Money amount)
    {
        if (Status != BookingStatus.Completed)
            return Result.Failure(BookingsErrors.InvalidState);

        if (amount == null || amount.Amount <= 0)
            return Result.Failure(BookingsErrors.InvalidFee);

        _lineItems.Add(new BookingLineItem(Guid.CreateVersion7(), Id, BookingLineItemType.OverKmFee, amount));
        return Result.Success();
    }

    private static bool IsTerminalState(BookingStatus status) =>
        status is BookingStatus.Completed or BookingStatus.Cancelled or BookingStatus.Rejected or BookingStatus.Expired;
}
