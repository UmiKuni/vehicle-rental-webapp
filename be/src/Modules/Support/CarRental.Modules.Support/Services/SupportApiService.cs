using CarRental.BuildingBlocks.Messaging;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Bookings.Contracts;
using CarRental.Modules.Payments.Contracts;
using CarRental.Modules.Support.Contracts;
using CarRental.Modules.Support.Contracts.Dtos;
using CarRental.Modules.Support.Data;
using CarRental.Modules.Support.Entities;
using CarRental.Modules.Support.Errors;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Support.Services;

internal sealed class SupportApiService : ISupportApi
{
    private readonly SupportDbContext _dbContext;
    private readonly IBookingApi _bookingApi;
    private readonly IPaymentsApi _paymentsApi;
    private readonly IEventOutbox _eventOutbox;
    private readonly TimeProvider _clock;

    public SupportApiService(
        SupportDbContext dbContext,
        IBookingApi bookingApi,
        IPaymentsApi paymentsApi,
        IEventOutbox eventOutbox,
        TimeProvider clock)
    {
        _dbContext = dbContext;
        _bookingApi = bookingApi;
        _paymentsApi = paymentsApi;
        _eventOutbox = eventOutbox;
        _clock = clock;
    }

    public async Task<Result<SupportTicketDto>> OpenAsync(
        Guid reporterId,
        OpenSupportTicketDto request,
        CancellationToken ct = default)
    {
        var participant = await _bookingApi.IsParticipantAsync(request.BookingId, reporterId, ct);
        if (!participant)
            return Result.Failure<SupportTicketDto>(SupportErrors.NotBookingParticipant);

        var freezeResult = await _paymentsApi.FreezeDepositAsync(request.BookingId, ct);
        if (freezeResult.IsFailure)
            return Result.Failure<SupportTicketDto>(freezeResult.Error);

        var ticketResult = SupportTicket.Open(
            request.BookingId,
            reporterId,
            request.Message,
            request.AttachmentKey,
            _clock.GetUtcNow());
        if (ticketResult.IsFailure)
            return Result.Failure<SupportTicketDto>(ticketResult.Error);

        _dbContext.SupportTickets.Add(ticketResult.Value);
        await _dbContext.SaveChangesAsync(ct);
        await PublishDomainEventsAsync(ticketResult.Value, ct);
        return Result.Success(ToDto(ticketResult.Value));
    }

    public async Task<Result<PagedResult<SupportTicketDto>>> GetMyTicketsAsync(
        Guid userId,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return Result.Failure<PagedResult<SupportTicketDto>>(SupportErrors.InvalidPaging);

        var query = _dbContext.SupportTickets
            .AsNoTracking()
            .Where(item => item.ReporterId == userId)
            .OrderByDescending(item => item.UpdatedAt);

        var totalCount = await query.CountAsync(ct);
        var tickets = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result.Success(new PagedResult<SupportTicketDto>(
            tickets.Select(ToDto).ToList(),
            totalCount,
            page,
            pageSize));
    }

    public async Task<Result<SupportTicketDto>> GetByIdAsync(
        Guid ticketId,
        Guid actorId,
        bool isAdmin = false,
        CancellationToken ct = default)
    {
        var ticket = await FindTicketAsync(ticketId, ct);
        if (ticket is null)
            return Result.Failure<SupportTicketDto>(SupportErrors.NotFound);

        if (!isAdmin && !await CanAccessAsync(ticket, actorId, ct))
            return Result.Failure<SupportTicketDto>(SupportErrors.Forbidden);

        return Result.Success(ToDto(ticket));
    }

    public async Task<Result<SupportTicketDto>> AddMessageAsync(
        Guid ticketId,
        Guid senderId,
        AddTicketMessageDto request,
        bool isAdmin = false,
        CancellationToken ct = default)
    {
        var ticket = await FindTicketAsync(ticketId, ct);
        if (ticket is null)
            return Result.Failure<SupportTicketDto>(SupportErrors.NotFound);

        if (!isAdmin && !await CanAccessAsync(ticket, senderId, ct))
            return Result.Failure<SupportTicketDto>(SupportErrors.Forbidden);

        var result = ticket.AddMessage(
            senderId,
            request.Content,
            request.AttachmentKey,
            _clock.GetUtcNow());
        if (result.IsFailure)
            return Result.Failure<SupportTicketDto>(result.Error);

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success(ToDto(ticket));
    }

    public async Task<Result> StartReviewAsync(
        Guid ticketId,
        Guid adminId,
        CancellationToken ct = default)
    {
        var ticket = await FindTicketAsync(ticketId, ct);
        if (ticket is null)
            return Result.Failure(SupportErrors.NotFound);

        var result = ticket.StartReview(adminId, _clock.GetUtcNow());
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<SupportTicketDto>> ResolveAsync(
        Guid ticketId,
        Guid adminId,
        ResolveSupportTicketDto request,
        CancellationToken ct = default)
    {
        var ticket = await FindTicketAsync(ticketId, ct);
        if (ticket is null)
            return Result.Failure<SupportTicketDto>(SupportErrors.NotFound);

        var settleResult = await _paymentsApi.SettleDepositAsync(
            ticket.BookingId,
            request.DepositToCustomerPercent,
            ct);
        if (settleResult.IsFailure)
            return Result.Failure<SupportTicketDto>(settleResult.Error);

        var result = ticket.Resolve(
            adminId,
            request.DepositToCustomerPercent,
            request.Note,
            _clock.GetUtcNow());
        if (result.IsFailure)
            return Result.Failure<SupportTicketDto>(result.Error);

        await _dbContext.SaveChangesAsync(ct);
        await PublishDomainEventsAsync(ticket, ct);
        return Result.Success(ToDto(ticket));
    }

    private async Task<SupportTicket?> FindTicketAsync(Guid ticketId, CancellationToken ct) =>
        await _dbContext.SupportTickets
            .FirstOrDefaultAsync(item => item.Id == TicketId.From(ticketId), ct);

    private async Task<bool> CanAccessAsync(
        SupportTicket ticket,
        Guid actorId,
        CancellationToken ct) =>
        ticket.ReporterId == actorId ||
        await _bookingApi.IsParticipantAsync(ticket.BookingId, actorId, ct);

    private async Task PublishDomainEventsAsync(SupportTicket ticket, CancellationToken ct)
    {
        var events = ticket.DomainEvents.OfType<IIntegrationEvent>().ToList();
        ticket.ClearDomainEvents();
        await _eventOutbox.PublishRangeAsync(events, ct);
    }

    private static SupportTicketDto ToDto(SupportTicket ticket) =>
        new(
            ticket.Id.Value,
            ticket.BookingId,
            ticket.ReporterId,
            ticket.Status,
            ticket.DepositToCustomerPercent,
            ticket.ResolutionNote,
            ticket.ResolvedBy,
            ticket.ResolvedAt,
            ticket.CreatedAt,
            ticket.UpdatedAt,
            ticket.Messages
                .OrderBy(message => message.CreatedAt)
                .Select(message => new TicketMessageDto(
                    message.Id,
                    message.SenderId,
                    message.Content,
                    message.AttachmentKey,
                    message.CreatedAt))
                .ToList());
}
