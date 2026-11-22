using System.Security.Claims;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Audit.Contracts;
using CarRental.Modules.Support.Contracts;
using CarRental.Modules.Support.Contracts.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Support.Endpoints;

public static class SupportEndpoints
{
    public static IEndpointRouteBuilder MapSupportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support")
            .WithTags("Support")
            .RequireAuthorization("AuthenticatedUser");

        group.MapPost("/tickets", async (
            OpenSupportTicketDto request,
            ClaimsPrincipal principal,
            ISupportApi api,
            CancellationToken ct) =>
            (await api.OpenAsync(GetUserId(principal), request, ct)).ToCreatedHttpResult(
                ticket => $"/api/support/tickets/{ticket.TicketId}"))
            .Produces<SupportTicketDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/tickets", async (
            ClaimsPrincipal principal,
            ISupportApi api,
            CancellationToken ct,
            int page = 1,
            int pageSize = 10) =>
            (await api.GetMyTicketsAsync(GetUserId(principal), page, pageSize, ct)).ToHttpResult())
            .Produces<PagedResult<SupportTicketDto>>(StatusCodes.Status200OK);

        group.MapGet("/tickets/{ticketId:guid}", async (
            Guid ticketId,
            ClaimsPrincipal principal,
            ISupportApi api,
            CancellationToken ct) =>
            (await api.GetByIdAsync(ticketId, GetUserId(principal), principal.IsInRole("Admin"), ct)).ToHttpResult())
            .Produces<SupportTicketDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/tickets/{ticketId:guid}/messages", async (
            Guid ticketId,
            AddTicketMessageDto request,
            ClaimsPrincipal principal,
            ISupportApi api,
            CancellationToken ct) =>
            (await api.AddMessageAsync(
                ticketId,
                GetUserId(principal),
                request,
                principal.IsInRole("Admin"),
                ct)).ToHttpResult())
            .Produces<SupportTicketDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/tickets/{ticketId:guid}/review", async (
            Guid ticketId,
            ClaimsPrincipal principal,
            ISupportApi api,
            IAuditRecorder auditRecorder,
            CancellationToken ct) =>
        {
            var adminId = GetUserId(principal);
            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "StartReviewTicket",
                entityName: "SupportTicket",
                entityId: ticketId.ToString(),
                execute: () => api.StartReviewAsync(ticketId, adminId, ct),
                captureBeforeState: async () => (await api.GetByIdAsync(ticketId, adminId, isAdmin: true, ct)).Value,
                captureAfterState: () => new { Status = "UnderReview", ReviewedBy = adminId },
                actorId: adminId,
                ct: ct);
            return result.ToHttpResult();
        })
            .RequireAuthorization("AdminOnly")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPut("/tickets/{ticketId:guid}/resolve", async (
            Guid ticketId,
            ResolveSupportTicketDto request,
            ClaimsPrincipal principal,
            ISupportApi api,
            IAuditRecorder auditRecorder,
            CancellationToken ct) =>
        {
            var adminId = GetUserId(principal);
            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "ResolveTicket",
                entityName: "SupportTicket",
                getEntityId: dto => dto.TicketId.ToString(),
                execute: () => api.ResolveAsync(ticketId, adminId, request, ct),
                captureBeforeState: async () => (await api.GetByIdAsync(ticketId, adminId, isAdmin: true, ct)).Value,
                captureAfterState: dto => new { dto.TicketId, dto.Status, dto.DepositToCustomerPercent, dto.ResolutionNote, dto.ResolvedBy },
                actorId: adminId,
                ct: ct);
            return result.ToHttpResult();
        })
            .RequireAuthorization("AdminOnly")
            .Produces<SupportTicketDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static Guid GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : throw new UnauthorizedAccessException("The authenticated user identifier is invalid.");
}
