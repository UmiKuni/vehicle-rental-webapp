using CarRental.Modules.Fleet.Contracts;
using CarRental.Modules.Fleet.Contracts.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace CarRental.Modules.Fleet.Endpoints;

public static class FleetEndpoints
{
    public static IEndpointRouteBuilder MapFleetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/fleet").WithTags("Fleet");

        group.MapGet("/brands", async (IFleetApi api, CancellationToken ct) =>
            Results.Ok((await api.GetBrandsAsync(ct)).Value));

        group.MapGet("/brands/{brandId:guid}", async (Guid brandId, IFleetApi api, CancellationToken ct) =>
            EntityResult(await api.GetBrandAsync(brandId, ct)));

        group.MapPost("/brands", async (CreateBrandDto dto, IFleetApi api, CancellationToken ct) =>
            CreatedResult(await api.CreateBrandAsync(dto, ct), "brands"))
            .RequireAuthorization("AdminOnly");

        group.MapPut("/brands/{brandId:guid}", async (Guid brandId, UpdateBrandDto dto, IFleetApi api, CancellationToken ct) =>
            EntityResult(await api.UpdateBrandAsync(brandId, dto, ct)))
            .RequireAuthorization("AdminOnly");

        group.MapDelete("/brands/{brandId:guid}", async (Guid brandId, IFleetApi api, CancellationToken ct) =>
            ActionResult(await api.DeleteBrandAsync(brandId, ct)))
            .RequireAuthorization("AdminOnly");

        group.MapGet("/models", async (Guid? brandId, IFleetApi api, CancellationToken ct) =>
            Results.Ok((await api.GetModelsAsync(brandId, ct)).Value));

        group.MapGet("/models/{modelId:guid}", async (Guid modelId, IFleetApi api, CancellationToken ct) =>
            EntityResult(await api.GetModelAsync(modelId, ct)));

        group.MapPost("/models", async (CreateModelDto dto, IFleetApi api, CancellationToken ct) =>
            CreatedResult(await api.CreateModelAsync(dto, ct), "models"))
            .RequireAuthorization("AdminOnly");

        group.MapPut("/models/{modelId:guid}", async (Guid modelId, UpdateModelDto dto, IFleetApi api, CancellationToken ct) =>
            EntityResult(await api.UpdateModelAsync(modelId, dto, ct)))
            .RequireAuthorization("AdminOnly");

        group.MapDelete("/models/{modelId:guid}", async (Guid modelId, IFleetApi api, CancellationToken ct) =>
            ActionResult(await api.DeleteModelAsync(modelId, ct)))
            .RequireAuthorization("AdminOnly");

        group.MapGet("/vehicles", async (IFleetApi api, CancellationToken ct) =>
        {
            var result = await api.GetPublishedVehiclesAsync(ct);
            return Results.Ok(result.Value);
        });

        group.MapGet("/vehicles/{vehicleId:guid}", async (Guid vehicleId, IFleetApi api, CancellationToken ct) =>
        {
            var result = await api.GetVehicleSummaryAsync(vehicleId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error.Message);
        });

        group.MapGet("/owners/{ownerId:guid}/vehicles", async (Guid ownerId, IFleetApi api, CancellationToken ct) =>
        {
            var result = await api.GetOwnerVehiclesAsync(ownerId, ct);
            return Results.Ok(result.Value);
        });

        group.MapPost("/vehicles", async (CreateVehicleDto dto, IFleetApi api, CancellationToken ct) =>
        {
            var result = await api.CreateVehicleAsync(dto, ct);
            return result.IsSuccess
                ? Results.Created($"/api/fleet/vehicles/{result.Value.VehicleId}", result.Value)
                : Results.BadRequest(result.Error.Message);
        }).RequireAuthorization("OwnerOnly");

        group.MapPut("/vehicles/{vehicleId:guid}", async (Guid vehicleId, UpdateVehicleDto dto, IFleetApi api, CancellationToken ct) =>
        {
            var result = await api.UpdateVehicleAsync(vehicleId, dto, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error.Message);
        }).RequireAuthorization("OwnerOnly");

        group.MapPost("/vehicles/{vehicleId:guid}/submit", async (Guid vehicleId, IFleetApi api, CancellationToken ct) =>
            ActionResult(await api.SubmitForApprovalAsync(vehicleId, ct)))
            .RequireAuthorization("OwnerOnly");

        group.MapPost("/vehicles/{vehicleId:guid}/approve", async (Guid vehicleId, ClaimsPrincipal principal, IFleetApi api, CancellationToken ct) =>
            ActionResult(await api.ApproveAsync(vehicleId, GetUserId(principal), ct)))
            .RequireAuthorization("AdminOnly");

        group.MapPost("/vehicles/{vehicleId:guid}/reject", async (Guid vehicleId, RejectVehicleDto dto, ClaimsPrincipal principal, IFleetApi api, CancellationToken ct) =>
            ActionResult(await api.RejectAsync(vehicleId, GetUserId(principal), dto, ct)))
            .RequireAuthorization("AdminOnly");

        group.MapPost("/vehicles/{vehicleId:guid}/hide", async (Guid vehicleId, IFleetApi api, CancellationToken ct) =>
            ActionResult(await api.HideAsync(vehicleId, ct)))
            .RequireAuthorization("OwnerOnly");

        group.MapPost("/vehicles/{vehicleId:guid}/show", async (Guid vehicleId, IFleetApi api, CancellationToken ct) =>
            ActionResult(await api.ShowAsync(vehicleId, ct)))
            .RequireAuthorization("OwnerOnly");

        group.MapPost("/vehicles/{vehicleId:guid}/unpublish", async (Guid vehicleId, UnpublishVehicleDto dto, ClaimsPrincipal principal, IFleetApi api, CancellationToken ct) =>
            ActionResult(await api.UnpublishAsync(vehicleId, GetUserId(principal), dto, ct)))
            .RequireAuthorization("AdminOnly");

        return app;
    }

    private static IResult ActionResult(CarRental.BuildingBlocks.Results.Result result) =>
        result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error.Message);

    private static IResult EntityResult<T>(CarRental.BuildingBlocks.Results.Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error.Message);

    private static IResult CreatedResult<T>(
        CarRental.BuildingBlocks.Results.Result<T> result,
        string resource)
    {
        return result.IsSuccess
            ? Results.Created($"/api/fleet/{resource}", result.Value)
            : Results.BadRequest(result.Error.Message);
    }

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("The authenticated user identifier is invalid.");
    }
}
