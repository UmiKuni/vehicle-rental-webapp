using CarRental.Modules.Support.Contracts.Enums;

namespace CarRental.Modules.Support.Contracts.Dtos;

public sealed record OpenSupportTicketDto(
    Guid BookingId,
    string Message,
    string? AttachmentKey = null);

public sealed record AddTicketMessageDto(
    string Content,
    string? AttachmentKey = null);

public sealed record ResolveSupportTicketDto(
    int DepositToCustomerPercent,
    string Note);

public sealed record TicketMessageDto(
    Guid MessageId,
    Guid SenderId,
    string Content,
    string? AttachmentKey,
    DateTimeOffset CreatedAt);

public sealed record SupportTicketDto(
    Guid TicketId,
    Guid BookingId,
    Guid ReporterId,
    TicketStatus Status,
    int? DepositToCustomerPercent,
    string? ResolutionNote,
    Guid? ResolvedBy,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<TicketMessageDto> Messages);
