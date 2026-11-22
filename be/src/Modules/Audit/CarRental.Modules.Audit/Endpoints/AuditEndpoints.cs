using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Audit.Data.Repositories;
using CarRental.Modules.Audit.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Audit.Endpoints;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit")
            .WithTags("Audit")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/", async (
            IAuditLogRepository repository,
            int page = 1,
            int pageSize = 10,
            Guid? actorId = null,
            string? entityName = null,
            string? entityId = null,
            string? action = null,
            DateTimeOffset? from = null,
            DateTimeOffset? to = null,
            CancellationToken ct = default) =>
        {
            var normalizedPage = page < 1 ? 1 : page;
            var normalizedPageSize = pageSize is < 1 or > 100 ? 10 : pageSize;
            var (items, totalCount) =
                await repository.GetPagedAsync(
                    normalizedPage,
                    normalizedPageSize,
                    actorId,
                    entityName,
                    entityId,
                    action,
                    from,
                    to,
                    ct);

            return Results.Ok(new PagedResult<AuditLogEntry>(
                items,
                totalCount,
                normalizedPage,
                normalizedPageSize));
        })
        .WithName("GetPagedAuditEntries")
        .WithSummary("Get audit entries paged with optional filters")
        .Produces<PagedResult<AuditLogEntry>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (
            Guid id,
            IAuditLogRepository repository,
            CancellationToken ct) =>
        {
            var entry = await repository.GetByIdAsync(id, ct);
            return entry is null
                ? Results.NotFound()
                : Results.Ok(entry);
        })
        .WithName("GetAuditEntry")
        .WithSummary("Get an audit entry by ID");

        group.MapGet("/entity/{entityName}/{entityId}", async (
            string entityName,
            string entityId,
            IAuditLogRepository repository,
            CancellationToken ct) =>
            Results.Ok(await repository.GetByEntityAsync(entityName, entityId, ct)))
        .WithName("GetAuditHistoryByEntity")
        .WithSummary("Get audit history for an entity");

        group.MapGet("/actor/{actorId:guid}", async (
            Guid actorId,
            IAuditLogRepository repository,
            CancellationToken ct) =>
            Results.Ok(await repository.GetByActorAsync(actorId, ct)))
        .WithName("GetAuditHistoryByActor")
        .WithSummary("Get audit history for an actor");

        return app;
    }
}
