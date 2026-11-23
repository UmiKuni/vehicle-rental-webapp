using System.Security.Claims;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Audit.Contracts;
using CarRental.Modules.Fleet.Contracts;
using CarRental.Modules.Fleet.Contracts.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Fleet.Endpoints;

public static class FleetEndpoints
{
    public static IEndpointRouteBuilder MapFleetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/fleet").WithTags("Fleet");

        group.MapGet("/brands", async (IFleetApi api, CancellationToken ct) =>
            (await api.GetBrandsAsync(ct)).ToHttpResult())
            .Produces<IReadOnlyList<BrandDto>>(StatusCodes.Status200OK);

        group.MapGet("/brands/{brandId:guid}", async (Guid brandId, IFleetApi api, CancellationToken ct) =>
            (await api.GetBrandAsync(brandId, ct)).ToHttpResult())
            .Produces<BrandDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/brands", async (CreateBrandDto dto, ClaimsPrincipal principal, IFleetApi api, IAuditRecorder auditRecorder, CancellationToken ct) =>
        {
            var adminId = GetUserId(principal);
            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "CreateBrand",
                entityName: "Brand",
                getEntityId: b => b.BrandId.ToString(),
                execute: () => api.CreateBrandAsync(dto, ct),
                captureAfterState: b => new { b.BrandId, b.Name },
                actorId: adminId,
                ct: ct);
            return result.ToCreatedHttpResult(b => $"/api/fleet/brands/{b.BrandId}");
        })
            .RequireAuthorization("AdminOnly")
            .Produces<BrandDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/brands/{brandId:guid}", async (Guid brandId, UpdateBrandDto dto, ClaimsPrincipal principal, IFleetApi api, IAuditRecorder auditRecorder, CancellationToken ct) =>
        {
            var adminId = GetUserId(principal);
            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "UpdateBrand",
                entityName: "Brand",
                getEntityId: b => b.BrandId.ToString(),
                execute: () => api.UpdateBrandAsync(brandId, dto, ct),
                captureBeforeState: async () => (await api.GetBrandAsync(brandId, ct)).Value,
                captureAfterState: b => new { b.BrandId, b.Name },
                actorId: adminId,
                ct: ct);
            return result.ToHttpResult();
        })
            .RequireAuthorization("AdminOnly")
            .Produces<BrandDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/brands/{brandId:guid}", async (Guid brandId, ClaimsPrincipal principal, IFleetApi api, IAuditRecorder auditRecorder, CancellationToken ct) =>
        {
            var adminId = GetUserId(principal);
            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "DeleteBrand",
                entityName: "Brand",
                entityId: brandId.ToString(),
                execute: () => api.DeleteBrandAsync(brandId, ct),
                captureBeforeState: async () => (await api.GetBrandAsync(brandId, ct)).Value,
                actorId: adminId,
                ct: ct);
            return result.ToHttpResult();
        })
            .RequireAuthorization("AdminOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/models", async (Guid? brandId, IFleetApi api, CancellationToken ct) =>
            (await api.GetModelsAsync(brandId, ct)).ToHttpResult())
            .Produces<IReadOnlyList<ModelDto>>(StatusCodes.Status200OK);

        group.MapGet("/models/{modelId:guid}", async (Guid modelId, IFleetApi api, CancellationToken ct) =>
            (await api.GetModelAsync(modelId, ct)).ToHttpResult())
            .Produces<ModelDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/models", async (CreateModelDto dto, ClaimsPrincipal principal, IFleetApi api, IAuditRecorder auditRecorder, CancellationToken ct) =>
        {
            var adminId = GetUserId(principal);
            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "CreateModel",
                entityName: "VehicleModel",
                getEntityId: m => m.ModelId.ToString(),
                execute: () => api.CreateModelAsync(dto, ct),
                captureAfterState: m => new { m.ModelId, m.BrandId, m.Name },
                actorId: adminId,
                ct: ct);
            return result.ToCreatedHttpResult(m => $"/api/fleet/models/{m.ModelId}");
        })
            .RequireAuthorization("AdminOnly")
            .Produces<ModelDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/models/{modelId:guid}", async (Guid modelId, UpdateModelDto dto, ClaimsPrincipal principal, IFleetApi api, IAuditRecorder auditRecorder, CancellationToken ct) =>
        {
            var adminId = GetUserId(principal);
            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "UpdateModel",
                entityName: "VehicleModel",
                getEntityId: m => m.ModelId.ToString(),
                execute: () => api.UpdateModelAsync(modelId, dto, ct),
                captureBeforeState: async () => (await api.GetModelAsync(modelId, ct)).Value,
                captureAfterState: m => new { m.ModelId, m.Name },
                actorId: adminId,
                ct: ct);
            return result.ToHttpResult();
        })
            .RequireAuthorization("AdminOnly")
            .Produces<ModelDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/models/{modelId:guid}", async (Guid modelId, ClaimsPrincipal principal, IFleetApi api, IAuditRecorder auditRecorder, CancellationToken ct) =>
        {
            var adminId = GetUserId(principal);
            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "DeleteModel",
                entityName: "VehicleModel",
                entityId: modelId.ToString(),
                execute: () => api.DeleteModelAsync(modelId, ct),
                captureBeforeState: async () => (await api.GetModelAsync(modelId, ct)).Value,
                actorId: adminId,
                ct: ct);
            return result.ToHttpResult();
        })
            .RequireAuthorization("AdminOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/vehicles", async (IFleetApi api, CancellationToken ct) =>
            (await api.GetPublishedVehiclesAsync(ct)).ToHttpResult())
            .Produces<IReadOnlyList<VehicleSummaryDto>>(StatusCodes.Status200OK);

        group.MapGet("/vehicles/{vehicleId:guid}", async (Guid vehicleId, IFleetApi api, CancellationToken ct) =>
            (await api.GetVehicleSummaryAsync(vehicleId, ct)).ToHttpResult())
            .Produces<VehicleSummaryDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/owners/{ownerId:guid}/vehicles", async (Guid ownerId, IFleetApi api, CancellationToken ct) =>
            (await api.GetOwnerVehiclesAsync(ownerId, ct)).ToHttpResult())
            .Produces<IReadOnlyList<VehicleSummaryDto>>(StatusCodes.Status200OK);

        group.MapPost("/vehicles", async (CreateVehicleDto dto, IFleetApi api, CancellationToken ct) =>
            (await api.CreateVehicleAsync(dto, ct)).ToCreatedHttpResult(v => $"/api/fleet/vehicles/{v.VehicleId}"))
            .RequireAuthorization("OwnerOnly")
            .Produces<VehicleSummaryDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/vehicles/{vehicleId:guid}", async (Guid vehicleId, UpdateVehicleDto dto, IFleetApi api, CancellationToken ct) =>
            (await api.UpdateVehicleAsync(vehicleId, dto, ct)).ToHttpResult())
            .RequireAuthorization("OwnerOnly")
            .Produces<VehicleSummaryDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/vehicles/{vehicleId:guid}/submit", async (Guid vehicleId, IFleetApi api, CancellationToken ct) =>
            (await api.SubmitForApprovalAsync(vehicleId, ct)).ToHttpResult())
            .RequireAuthorization("OwnerOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/vehicles/{vehicleId:guid}/approve", async (Guid vehicleId, ClaimsPrincipal principal, IFleetApi api, IAuditRecorder auditRecorder, CancellationToken ct) =>
        {
            var adminId = GetUserId(principal);
            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "ApproveVehicle",
                entityName: "Vehicle",
                entityId: vehicleId.ToString(),
                execute: () => api.ApproveAsync(vehicleId, adminId, ct),
                captureBeforeState: async () => (await api.GetVehicleSummaryAsync(vehicleId, ct)).Value,
                captureAfterState: () => new { Status = "Approved", ApprovedBy = adminId },
                actorId: adminId,
                ct: ct);
            return result.ToHttpResult();
        })
            .RequireAuthorization("AdminOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/vehicles/{vehicleId:guid}/reject", async (Guid vehicleId, RejectVehicleDto dto, ClaimsPrincipal principal, IFleetApi api, IAuditRecorder auditRecorder, CancellationToken ct) =>
        {
            var adminId = GetUserId(principal);
            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "RejectVehicle",
                entityName: "Vehicle",
                entityId: vehicleId.ToString(),
                execute: () => api.RejectAsync(vehicleId, adminId, dto, ct),
                captureBeforeState: async () => (await api.GetVehicleSummaryAsync(vehicleId, ct)).Value,
                captureAfterState: () => new { Status = "Rejected", RejectedBy = adminId, dto.Reason },
                actorId: adminId,
                ct: ct);
            return result.ToHttpResult();
        })
            .RequireAuthorization("AdminOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/vehicles/{vehicleId:guid}/hide", async (Guid vehicleId, IFleetApi api, CancellationToken ct) =>
            (await api.HideAsync(vehicleId, ct)).ToHttpResult())
            .RequireAuthorization("OwnerOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/vehicles/{vehicleId:guid}/show", async (Guid vehicleId, IFleetApi api, CancellationToken ct) =>
            (await api.ShowAsync(vehicleId, ct)).ToHttpResult())
            .RequireAuthorization("OwnerOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/vehicles/{vehicleId:guid}/unpublish", async (Guid vehicleId, UnpublishVehicleDto dto, ClaimsPrincipal principal, IFleetApi api, IAuditRecorder auditRecorder, CancellationToken ct) =>
        {
            var adminId = GetUserId(principal);
            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "UnpublishVehicle",
                entityName: "Vehicle",
                entityId: vehicleId.ToString(),
                execute: () => api.UnpublishAsync(vehicleId, adminId, dto, ct),
                captureBeforeState: async () => (await api.GetVehicleSummaryAsync(vehicleId, ct)).Value,
                captureAfterState: () => new { Status = "Draft", UnpublishedBy = adminId, dto.Reason },
                actorId: adminId,
                ct: ct);
            return result.ToHttpResult();
        })
            .RequireAuthorization("AdminOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/vehicles/{vehicleId:guid}/images", async (
            Guid vehicleId,
            IFormFile file,
            ClaimsPrincipal principal,
            IFleetApi api,
            CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await api.UploadImageAsync(
                vehicleId,
                GetUserId(principal),
                stream,
                file.FileName,
                file.ContentType,
                ct);
            return result.ToHttpResult();
        })
        .DisableAntiforgery()
        .RequireAuthorization("OwnerOnly")
        .Produces<VehicleImageDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/vehicles/{vehicleId:guid}/images/{imageId:guid}", async (
            Guid vehicleId,
            Guid imageId,
            ClaimsPrincipal principal,
            IFleetApi api,
            CancellationToken ct) =>
            (await api.RemoveImageAsync(vehicleId, GetUserId(principal), imageId, ct)).ToHttpResult())
            .RequireAuthorization("OwnerOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("The authenticated user identifier is invalid.");
    }
}
