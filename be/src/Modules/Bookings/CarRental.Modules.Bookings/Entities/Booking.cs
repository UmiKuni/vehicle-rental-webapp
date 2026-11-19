using CarRental.Modules.Bookings.Errors;
using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Bookings.Contracts.Enums;

namespace CarRental.Modules.Bookings.Entities;

internal class Booking : AggregateRoot<BookingId>
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
        DateTimeOffset now,
        BookingId? bookingId = null)
    {
        if (customerId == Guid.Empty)
            return BookingsErrors.InvalidCustomer;

        if (ownerId == Guid.Empty)
            return BookingsErrors.InvalidOwner;

        if (vehicleId == Guid.Empty)
            return BookingsErrors.InvalidVehicle;

        if (customerId == ownerId)
            return BookingsErrors.CustomerIsOwner;

        if (period == null)
            return BookingsErrors.InvalidPeriod;

        if (period.Start <= now)
            return BookingsErrors.StartInPast;

        if (period.End <= period.Start)
            return BookingsErrors.InvalidDuration;

        if (string.IsNullOrWhiteSpace(pickupLocation))
            return BookingsErrors.InvalidLocation;

        if (lineItems == null || lineItems.Count == 0)
            return BookingsErrors.MissingLineItems;

        if (!lineItems.Any(l => l.Type == BookingLineItemType.Rent))
            return BookingsErrors.MissingRentItem;

        if (!lineItems.Any(l => l.Type == BookingLineItemType.Deposit))
            return BookingsErrors.MissingDepositItem;

        if (policy == null)
            return BookingsErrors.MissingPolicy;

        var id = bookingId ?? BookingId.New();
        return new Booking(
            id,
            customerId,
            ownerId,
            vehicleId,
            period,
            pickupLocation,
            lineItems,
            policy,
            now);
    }

    public Result Approve(Guid ownerId, DateTimeOffset now)
    {
        if (ownerId != OwnerId)
            return BookingsErrors.NotOwner;

        if (Status != BookingStatus.Pending)
            return BookingsErrors.InvalidState(nameof(BookingStatus.Pending), Status);

        Status = BookingStatus.Confirmed;
        PaymentDeadline = now.AddHours(2);
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id, BookingStatus.Confirmed, now, "Approved by owner"));
        return Result.Success();
    }

    public Result Reject(Guid ownerId, string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return BookingsErrors.ReasonRequired;

        if (ownerId != OwnerId)
            return BookingsErrors.NotOwner;

        if (Status != BookingStatus.Pending)
            return BookingsErrors.InvalidState(nameof(BookingStatus.Pending), Status);

        Status = BookingStatus.Rejected;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id, BookingStatus.Rejected, now, reason.Trim()));
        return Result.Success();
    }

    public Result MarkPaid(Guid paymentId, DateTimeOffset now)
    {
        if (Status != BookingStatus.Confirmed)
            return BookingsErrors.InvalidState(nameof(BookingStatus.Confirmed), Status);

        Status = BookingStatus.Paid;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id, BookingStatus.Paid, now, $"Payment marked paid (Payment ID: {paymentId})"));
        return Result.Success();
    }

    public Result<CancellationOutcome> Cancel(Guid actorId, string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return BookingsErrors.ReasonRequired;

        if (actorId != CustomerId)
            return BookingsErrors.NotCustomer;

        if (IsTerminalState(Status))
            return BookingsErrors.TerminalState(Status);

        if (Status == BookingStatus.InProgress)
            return BookingsErrors.TripInProgress;

        var penalty = Money.Zero();
        var refundable = Money.Zero();

        if (Status == BookingStatus.Paid)
        {
            var rentTotal = SumLineItems(BookingLineItemType.Rent);
            var depositTotal = SumLineItems(BookingLineItemType.Deposit);
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

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id, BookingStatus.Cancelled, now, reason.Trim()));
        return new CancellationOutcome(penalty, refundable);
    }

    public Result AdminCancel(Guid adminId, string reason, DateTimeOffset now)
    {
        if (adminId == Guid.Empty)
            return BookingsErrors.InvalidAdmin;

        if (string.IsNullOrWhiteSpace(reason))
            return BookingsErrors.ReasonRequired;

        if (IsTerminalState(Status))
            return BookingsErrors.TerminalState(Status);

        Status = BookingStatus.Cancelled;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id, BookingStatus.Cancelled, now, $"Admin cancel: {reason.Trim()}"));
        return Result.Success();
    }

    public Result Expire(string reason, DateTimeOffset now)
    {
        if (Status != BookingStatus.Pending && Status != BookingStatus.Confirmed)
            return BookingsErrors.InvalidState($"{nameof(BookingStatus.Pending)} or {nameof(BookingStatus.Confirmed)}", Status);

        Status = BookingStatus.Expired;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id, BookingStatus.Expired, now, reason));
        return Result.Success();
    }

    public Result StartTrip(DateTimeOffset now)
    {
        if (Status != BookingStatus.Paid)
            return BookingsErrors.InvalidState(nameof(BookingStatus.Paid), Status);

        Status = BookingStatus.InProgress;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id, BookingStatus.InProgress, now, "Trip started"));
        return Result.Success();
    }

    public Result CompleteTrip(DateTimeOffset now)
    {
        if (Status != BookingStatus.InProgress)
            return BookingsErrors.InvalidState(nameof(BookingStatus.InProgress), Status);

        Status = BookingStatus.Completed;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id, BookingStatus.Completed, now, "Trip completed"));
        return Result.Success();
    }

    public Result AddOverKmFee(Money amount)
    {
        if (Status != BookingStatus.Completed)
            return BookingsErrors.OverKmFeeRequiresCompleted;

        if (amount == null || amount.Amount <= 0)
            return BookingsErrors.InvalidFee;

        _lineItems.Add(new BookingLineItem(Guid.CreateVersion7(), Id, BookingLineItemType.OverKmFee, amount));
        return Result.Success();
    }

    private Money SumLineItems(BookingLineItemType type) =>
        _lineItems
            .Where(l => l.Type == type)
            .Aggregate(Money.Zero(), (acc, item) => acc.Add(item.Amount));

    private static bool IsTerminalState(BookingStatus status) =>
        status is BookingStatus.Completed or BookingStatus.Cancelled or BookingStatus.Rejected or BookingStatus.Expired;
}
