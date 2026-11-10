using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
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
            (await api.GetPricingAsync(vehicleId, ct)).ToHttpResult())
            .WithName("GetVehiclePricing")
            .WithSummary("Get the active pricing configuration for a vehicle")
            .Produces<PricingPlanDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/quotes", async (
            PricingQuoteRequest request,
            IPricingApi api,
            CancellationToken ct) =>
        {
            var periodResult = DateTimeRange.Create(request.StartTime, request.EndTime);
            if (periodResult.IsFailure)
                return periodResult.ToHttpResult();

            return (await api.GetQuoteAsync(request.VehicleId, periodResult.Value, ct)).ToHttpResult();
        })
        .WithName("GetPricingQuote")
        .WithSummary("Calculate a rental quote for a vehicle and period")
        .Produces<QuoteDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/vehicles/{vehicleId:guid}", async (
            Guid vehicleId,
            ConfigurePricingDto request,
            IPricingApi api,
            CancellationToken ct) =>
            (await api.ConfigurePricingAsync(vehicleId, request, ct)).ToHttpResult())
            .WithName("ConfigureVehiclePricing")
            .WithSummary("Configure hourly/daily rental pricing, deposit, fees, kilometre limit, and conditions")
            .Produces<PricingPlanDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("OwnerOnly");

        return app;
    }
}
