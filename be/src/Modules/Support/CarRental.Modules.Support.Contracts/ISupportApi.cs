using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Support.Contracts.Dtos;

namespace CarRental.Modules.Support.Contracts;

public interface ISupportApi
{
    Task<Result<SupportTicketDto>> OpenAsync(
        Guid reporterId,
        OpenSupportTicketDto request,
        CancellationToken ct = default);

    Task<Result<PagedResult<SupportTicketDto>>> GetMyTicketsAsync(
        Guid userId,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default);

    Task<Result<SupportTicketDto>> GetByIdAsync(
        Guid ticketId,
        Guid actorId,
        bool isAdmin = false,
        CancellationToken ct = default);

    Task<Result<SupportTicketDto>> AddMessageAsync(
        Guid ticketId,
        Guid senderId,
        AddTicketMessageDto request,
        bool isAdmin = false,
        CancellationToken ct = default);

    Task<Result> StartReviewAsync(
        Guid ticketId,
        Guid adminId,
        CancellationToken ct = default);

    Task<Result<SupportTicketDto>> ResolveAsync(
        Guid ticketId,
        Guid adminId,
        ResolveSupportTicketDto request,
        CancellationToken ct = default);
}
