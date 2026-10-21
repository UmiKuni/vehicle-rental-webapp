using CarRental.BuildingBlocks.Domain;
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
            id.Value,
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
            return Result.Failure<SupportTicket>(new Error("support.invalid_booking", "Booking ID is required."));

        if (reporterId == Guid.Empty)
            return Result.Failure<SupportTicket>(new Error("support.invalid_reporter", "Reporter ID is required."));

        if (string.IsNullOrWhiteSpace(firstMessage))
            return Result.Failure<SupportTicket>(new Error("support.empty_message", "An initial message describing the issue is required."));

        return Result.Success(new SupportTicket(
            TicketId.New(),
            bookingId,
            reporterId,
            firstMessage,
            attachmentKey,
            now));
    }

    public Result AddMessage(Guid senderId, string content, string? attachmentKey, DateTimeOffset now)
    {
        if (Status == TicketStatus.Resolved)
            return Result.Failure(new Error("support.ticket_resolved", "Cannot add messages to a resolved ticket."));

        if (senderId == Guid.Empty)
            return Result.Failure(new Error("support.invalid_sender", "Sender ID is required."));

        if (string.IsNullOrWhiteSpace(content))
            return Result.Failure(new Error("support.empty_message", "Message content cannot be empty."));

        _messages.Add(new TicketMessage(
            Guid.CreateVersion7(),
            Id.Value,
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
            return Result.Failure(new Error("support.invalid_admin", "Admin ID is required."));

        if (Status != TicketStatus.Open)
            return Result.Failure(new Error("support.invalid_state", $"Only open tickets can be moved to in-progress. Current status: {Status}."));

        Status = TicketStatus.InProgress;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result Resolve(Guid adminId, int depositToCustomerPercent, string note, DateTimeOffset now)
    {
        if (adminId == Guid.Empty)
            return Result.Failure(new Error("support.invalid_admin", "Admin ID is required."));

        if (Status == TicketStatus.Resolved)
            return Result.Failure(new Error("support.already_resolved", "Ticket has already been resolved."));

        if (depositToCustomerPercent < 0 || depositToCustomerPercent > 100)
            return Result.Failure(new Error("support.invalid_percent", "Customer deposit refund percentage must be between 0 and 100."));

        if (string.IsNullOrWhiteSpace(note))
            return Result.Failure(new Error("support.missing_resolution_note", "Resolution ruling note is required."));

        Status = TicketStatus.Resolved;
        DepositToCustomerPercent = depositToCustomerPercent;
        ResolutionNote = note.Trim();
        ResolvedBy = adminId;
        ResolvedAt = now;
        UpdatedAt = now;

        return Result.Success();
    }
}
