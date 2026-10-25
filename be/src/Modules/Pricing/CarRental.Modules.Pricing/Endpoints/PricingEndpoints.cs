using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Pricing.Contracts;
using CarRental.Modules.Pricing.Contracts.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Pricing.Endpoints;

public static class PricingEndpoints
{
    public static IEndpointRouteBuilder MapPricingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pricing")
            .WithTags("Pricing");

        group.MapGet("/vehicles/{vehicleId:guid}", async (
            Guid vehicleId,
            IPricingApi api,
            CancellationToken ct) =>
            EntityResult(await api.GetPricingAsync(vehicleId, ct)))
            .WithName("GetVehiclePricing")
            .WithSummary("Get the active pricing configuration for a vehicle")
            .Produces<PricingPlanDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/quotes", async (
            PricingQuoteRequest request,
            IPricingApi api,
            CancellationToken ct) =>
        {
            var periodResult = DateTimeRange.Create(request.StartTime, request.EndTime);
            if (periodResult.IsFailure)
                return Results.BadRequest(periodResult.Error.Message);

            return EntityResult(await api.GetQuoteAsync(request.VehicleId, periodResult.Value, ct));
        })
        .WithName("GetPricingQuote")
        .WithSummary("Calculate a rental quote for a vehicle and period")
        .Produces<QuoteDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/vehicles/{vehicleId:guid}", async (
            Guid vehicleId,
            ConfigurePricingDto request,
            IPricingApi api,
            CancellationToken ct) =>
            EntityResult(await api.ConfigurePricingAsync(vehicleId, request, ct)))
            .WithName("ConfigureVehiclePricing")
            .WithSummary("Configure hourly/daily rental pricing, deposit, fees, kilometre limit, and conditions")
            .Produces<PricingPlanDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization("OwnerOnly");

        return app;
    }

    private static IResult EntityResult<T>(CarRental.BuildingBlocks.Results.Result<T> result) =>
        result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.Code == "pricing.not_found"
                ? Results.NotFound(result.Error.Message)
                : Results.BadRequest(result.Error.Message);
}
