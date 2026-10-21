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
        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), id.Value, BookingStatus.Pending, now, "Booking requested"));
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
            return Result.Failure<Booking>(new Error("booking.invalid_customer", "Customer ID is required."));

        if (ownerId == Guid.Empty)
            return Result.Failure<Booking>(new Error("booking.invalid_owner", "Owner ID is required."));

        if (vehicleId == Guid.Empty)
            return Result.Failure<Booking>(new Error("booking.invalid_vehicle", "Vehicle ID is required."));

        if (customerId == ownerId)
            return Result.Failure<Booking>(new Error("booking.customer_is_owner", "Customer cannot rent their own vehicle."));

        if (period == null)
            return Result.Failure<Booking>(new Error("booking.invalid_period", "Rental period is required."));

        if (period.Start <= now)
            return Result.Failure<Booking>(new Error("booking.start_in_past", "Rental start time must be in the future."));

        if (period.End <= period.Start)
            return Result.Failure<Booking>(new Error("booking.invalid_duration", "End time must be after start time."));

        if (string.IsNullOrWhiteSpace(pickupLocation))
            return Result.Failure<Booking>(new Error("booking.invalid_location", "Pickup location is required."));

        if (lineItems == null || lineItems.Count == 0)
            return Result.Failure<Booking>(new Error("booking.missing_line_items", "Line items are required."));

        if (!lineItems.Any(l => l.Type == BookingLineItemType.Rent))
            return Result.Failure<Booking>(new Error("booking.missing_rent_item", "At least one Rent line item is required."));

        if (!lineItems.Any(l => l.Type == BookingLineItemType.Deposit))
            return Result.Failure<Booking>(new Error("booking.missing_deposit_item", "At least one Deposit line item is required."));

        if (policy == null)
            return Result.Failure<Booking>(new Error("booking.missing_policy", "Cancellation policy snapshot is required."));

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
            return Result.Failure(new Error("booking.unauthorized", "Only the vehicle owner can approve this booking."));

        if (Status != BookingStatus.Pending)
            return Result.Failure(new Error("booking.invalid_state", $"Only pending bookings can be approved. Current status: {Status}."));

        Status = BookingStatus.Confirmed;
        PaymentDeadline = now.AddHours(2);
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id.Value, BookingStatus.Confirmed, now, "Approved by owner"));
        return Result.Success();
    }

    public Result Reject(Guid ownerId, string reason, DateTimeOffset now)
    {
        if (ownerId != OwnerId)
            return Result.Failure(new Error("booking.unauthorized", "Only the vehicle owner can reject this booking."));

        if (Status != BookingStatus.Pending)
            return Result.Failure(new Error("booking.invalid_state", $"Only pending bookings can be rejected. Current status: {Status}."));

        Status = BookingStatus.Rejected;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id.Value, BookingStatus.Rejected, now, reason));
        return Result.Success();
    }

    public Result MarkPaid(Guid paymentId, DateTimeOffset now)
    {
        if (Status != BookingStatus.Confirmed)
            return Result.Failure(new Error("booking.invalid_state", $"Only confirmed bookings can be marked paid. Current status: {Status}."));

        Status = BookingStatus.Paid;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id.Value, BookingStatus.Paid, now, $"Payment marked paid (Payment ID: {paymentId})"));
        return Result.Success();
    }

    public Result<CancellationOutcome> Cancel(Guid actorId, string reason, DateTimeOffset now)
    {
        if (actorId != CustomerId)
            return Result.Failure<CancellationOutcome>(new Error("booking.unauthorized", "Only the booking customer can cancel this booking."));

        if (IsTerminalState(Status))
            return Result.Failure<CancellationOutcome>(new Error("booking.terminal_state", $"Cannot cancel a booking in terminal state {Status}."));

        if (Status == BookingStatus.InProgress)
            return Result.Failure<CancellationOutcome>(new Error("booking.trip_in_progress", "Cannot cancel a trip that is currently in progress."));

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

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id.Value, BookingStatus.Cancelled, now, reason));
        return Result.Success(new CancellationOutcome(penalty, refundable));
    }

    public Result AdminCancel(Guid adminId, string reason, DateTimeOffset now)
    {
        if (adminId == Guid.Empty)
            return Result.Failure(new Error("booking.invalid_admin", "Admin ID is required."));

        if (IsTerminalState(Status))
            return Result.Failure(new Error("booking.terminal_state", $"Cannot cancel a booking in terminal state {Status}."));

        Status = BookingStatus.Cancelled;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id.Value, BookingStatus.Cancelled, now, $"Admin cancel: {reason}"));
        return Result.Success();
    }

    public Result Expire(string reason, DateTimeOffset now)
    {
        if (Status != BookingStatus.Pending && Status != BookingStatus.Confirmed)
            return Result.Failure(new Error("booking.invalid_state", $"Only pending or confirmed bookings can expire. Current status: {Status}."));

        Status = BookingStatus.Expired;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id.Value, BookingStatus.Expired, now, reason));
        return Result.Success();
    }

    public Result StartTrip(DateTimeOffset now)
    {
        if (Status != BookingStatus.Paid)
            return Result.Failure(new Error("booking.invalid_state", $"Trip can only be started from Paid status. Current status: {Status}."));

        Status = BookingStatus.InProgress;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id.Value, BookingStatus.InProgress, now, "Trip started"));
        return Result.Success();
    }

    public Result CompleteTrip(DateTimeOffset now)
    {
        if (Status != BookingStatus.InProgress)
            return Result.Failure(new Error("booking.invalid_state", $"Trip can only be completed from InProgress status. Current status: {Status}."));

        Status = BookingStatus.Completed;
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id.Value, BookingStatus.Completed, now, "Trip completed"));
        return Result.Success();
    }

    public Result AddOverKmFee(Money amount)
    {
        if (Status != BookingStatus.Completed)
            return Result.Failure(new Error("booking.invalid_state", "Over-km fee can only be added to completed bookings."));

        if (amount == null || amount.Amount <= 0)
            return Result.Failure(new Error("booking.invalid_fee", "Over-km fee amount must be greater than zero."));

        _lineItems.Add(new BookingLineItem(Guid.CreateVersion7(), Id.Value, BookingLineItemType.OverKmFee, amount));
        return Result.Success();
    }

    private static bool IsTerminalState(BookingStatus status) =>
        status is BookingStatus.Completed or BookingStatus.Cancelled or BookingStatus.Rejected or BookingStatus.Expired;
}
