using System.Security.Claims;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Availability.Contracts;
using CarRental.Modules.Availability.Contracts.Dtos;
using CarRental.Modules.Availability.Contracts.Enums;
using CarRental.Modules.Availability.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Availability.Endpoints;

public static class AvailabilityEndpoints
{
    public static IEndpointRouteBuilder MapAvailabilityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/availability")
            .WithTags("Availability");

        group.MapGet("/vehicles/{vehicleId:guid}/check", async (
            Guid vehicleId,
            DateTimeOffset? start,
            DateTimeOffset? end,
            IAvailabilityApi api,
            CancellationToken ct) =>
        {
            if (!start.HasValue || !end.HasValue)
                return Result.Failure(AvailabilityErrors.InvalidRange).ToHttpResult();

            var rangeResult = DateTimeRange.Create(start.Value, end.Value);
            if (rangeResult.IsFailure)
                return rangeResult.ToHttpResult();

            return (await api.CheckVehicleAvailabilityAsync(vehicleId, rangeResult.Value, ct)).ToHttpResult();
        })
        .WithName("CheckVehicleAvailability")
        .WithSummary("Check vehicle availability for a given date range")
        .WithDescription("Checks whether a vehicle is free to be rented within the requested time range or collides with existing bookings/holds/blocks.")
        .Produces<AvailabilityCheckResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/vehicles/{vehicleId:guid}/calendar", async (
            Guid vehicleId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            IAvailabilityApi api,
            CancellationToken ct) =>
            (await api.GetCalendarAsync(vehicleId, from, to, ct)).ToHttpResult())
        .WithName("GetVehicleCalendar")
        .WithSummary("Get vehicle calendar and blocked schedule entries")
        .WithDescription("Retrieves all booked periods, temporary holds, maintenance blocks, and owner-blocked dates for a vehicle within a date range.")
        .Produces<IReadOnlyList<CalendarEntryDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/vehicles/{vehicleId:guid}/blocks", async (
            Guid vehicleId,
            BlockScheduleRequest request,
            ClaimsPrincipal principal,
            IAvailabilityApi api,
            CancellationToken ct) =>
        {
            var rangeResult = DateTimeRange.Create(request.StartTime, request.EndTime);
            if (rangeResult.IsFailure)
                return rangeResult.ToHttpResult();

            var result = await api.BlockAsync(
                vehicleId,
                GetUserId(principal),
                rangeResult.Value,
                request.Reason,
                request.Type ?? CalendarEntryType.OwnerBlocked,
                ct);

            return result.ToCreatedHttpResult(_ => $"/api/availability/vehicles/{vehicleId}/calendar");
        })
        .WithName("BlockVehicleSchedule")
        .RequireAuthorization("OwnerOnly")
        .WithSummary("Block vehicle schedule manually")
        .WithDescription("Allows vehicle owners to manually block dates for personal use or scheduled maintenance.")
        .Produces<CalendarEntryDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/blocks/{blockId:guid}", async (
            Guid blockId,
            ClaimsPrincipal principal,
            IAvailabilityApi api,
            CancellationToken ct) =>
            (await api.UnblockAsync(blockId, GetUserId(principal), ct)).ToHttpResult())
        .WithName("UnblockVehicleSchedule")
        .RequireAuthorization("OwnerOnly")
        .WithSummary("Unblock previously blocked schedule")
        .WithDescription("Removes an owner-created block, restoring vehicle availability for bookings.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
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
