using CarRental.Modules.Support.Errors;
using CarRental.BuildingBlocks.Domain;
using CarRental.Modules.Support.Contracts.Events;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Support.Contracts.Enums;

namespace CarRental.Modules.Support.Entities;

public class SupportTicket : AggregateRoot<TicketId>
{
    private readonly List<TicketMessage> _messages = new();

    public Guid BookingId { get; private set; }
    public Guid ReporterId { get; private set; }
    public TicketStatus Status { get; private set; }
    public int? DepositToCustomerPercent { get; private set; }
    public string? ResolutionNote { get; private set; }
    public Guid? ResolvedBy { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<TicketMessage> Messages => _messages.AsReadOnly();

    private SupportTicket() { }

    private SupportTicket(
        TicketId id,
        Guid bookingId,
        Guid reporterId,
        string firstMessage,
        string? attachmentKey,
        DateTimeOffset now)
    {
        Id = id;
        BookingId = bookingId;
        ReporterId = reporterId;
        Status = TicketStatus.Open;
        CreatedAt = now;
        UpdatedAt = now;

        _messages.Add(new TicketMessage(
            Guid.CreateVersion7(),
            id,
            reporterId,
            firstMessage,
            attachmentKey,
            now));
    }

    public static Result<SupportTicket> Open(
        Guid bookingId,
        Guid reporterId,
        string firstMessage,
        string? attachmentKey,
        DateTimeOffset now)
    {
        if (bookingId == Guid.Empty)
            return Result.Failure<SupportTicket>(SupportErrors.InvalidBooking);

        if (reporterId == Guid.Empty)
            return Result.Failure<SupportTicket>(SupportErrors.InvalidReporter);

        if (string.IsNullOrWhiteSpace(firstMessage))
            return Result.Failure<SupportTicket>(SupportErrors.EmptyMessage98E994);

        var ticket = new SupportTicket(
            TicketId.New(),
            bookingId,
            reporterId,
            firstMessage,
            attachmentKey,
            now);

        ticket.Raise(new TicketOpened(
            Guid.CreateVersion7(),
            now,
            ticket.Id,
            bookingId,
            reporterId));

        return Result.Success(ticket);
    }

    public Result AddMessage(Guid senderId, string content, string? attachmentKey, DateTimeOffset now)
    {
        if (Status == TicketStatus.Resolved)
            return Result.Failure(SupportErrors.TicketResolved);

        if (senderId == Guid.Empty)
            return Result.Failure(SupportErrors.InvalidSender);

        if (string.IsNullOrWhiteSpace(content))
            return Result.Failure(SupportErrors.EmptyMessageE27D76);

        _messages.Add(new TicketMessage(
            Guid.CreateVersion7(),
            Id,
            senderId,
            content,
            attachmentKey,
            now));

        UpdatedAt = now;
        return Result.Success();
    }

    public Result StartReview(Guid adminId, DateTimeOffset now)
    {
        if (adminId == Guid.Empty)
            return Result.Failure(SupportErrors.InvalidAdmin);

        if (Status != TicketStatus.Open)
            return Result.Failure(Error.Conflict("support.invalid_state", $"Only open tickets can be moved to in-progress. Current status: {Status}."));

        Status = TicketStatus.InProgress;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result Resolve(Guid adminId, int depositToCustomerPercent, string note, DateTimeOffset now)
    {
        if (adminId == Guid.Empty)
            return Result.Failure(SupportErrors.InvalidAdmin);

        if (Status == TicketStatus.Resolved)
            return Result.Failure(SupportErrors.AlreadyResolved);

        if (depositToCustomerPercent < 0 || depositToCustomerPercent > 100)
            return Result.Failure(SupportErrors.InvalidPercent);

        if (string.IsNullOrWhiteSpace(note))
            return Result.Failure(SupportErrors.MissingResolutionNote);

        Status = TicketStatus.Resolved;
        DepositToCustomerPercent = depositToCustomerPercent;
        ResolutionNote = note.Trim();
        ResolvedBy = adminId;
        ResolvedAt = now;
        UpdatedAt = now;

        Raise(new TicketResolved(
            Guid.CreateVersion7(),
            now,
            Id,
            BookingId,
            depositToCustomerPercent,
            ResolutionNote,
            adminId));

        return Result.Success();
    }
}
