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

        // 1. Get vehicle daily base pricing
        group.MapGet("/vehicles/{vehicleId:guid}", (Guid vehicleId) =>
        {
            var mockResponse = new VehiclePricingDto(
                VehicleId: vehicleId,
                BaseDailyRate: 850_000m,
                WeekendDailyRate: 1_100_000m,
                SecurityDeposit: 5_000_000m,
                Currency: "VND",
                OverKmSurchargePerKm: 5_000m,
                DailyKmLimit: 300);

            return Results.Ok(mockResponse);
        })
        .WithName("GetVehiclePricing")
        .WithSummary("Get vehicle base rate and deposit requirements")
        .WithDescription("Retrieves the base daily rental price, weekend surcharge, and required security deposit for a specific vehicle.")
        .Produces<VehiclePricingDto>(StatusCodes.Status200OK);

        // 2. Estimate total cost for a rental duration
        group.MapPost("/estimate", (EstimatePriceRequest request) =>
        {
            if (request.EndDate <= request.StartDate)
            {
                return Results.BadRequest(new { error = "EndDate must be greater than StartDate." });
            }

            var days = Math.Max(1, (int)Math.Ceiling((request.EndDate - request.StartDate).TotalDays));
            decimal dailyRate = 850_000m;
            decimal baseRent = days * dailyRate;
            decimal insuranceFee = request.IncludeInsurance ? days * 120_000m : 0m;
            decimal weekendSurcharge = 250_000m;
            decimal deposit = 5_000_000m;
            decimal totalRent = baseRent + weekendSurcharge + insuranceFee;

            var estimate = new PriceEstimateDto(
                VehicleId: request.VehicleId,
                StartDate: request.StartDate,
                EndDate: request.EndDate,
                TotalDays: days,
                BaseRent: baseRent,
                WeekendSurcharge: weekendSurcharge,
                InsuranceFee: insuranceFee,
                SecurityDeposit: deposit,
                TotalEstimatedCost: totalRent + deposit,
                Currency: "VND");

            return Results.Ok(estimate);
        })
        .WithName("EstimateRentalCost")
        .WithSummary("Estimate rental cost for a period")
        .WithDescription("Calculates an estimated price breakdown including daily rent, weekend surcharges, optional insurance, and security deposit.")
        .Produces<PriceEstimateDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 3. Platform pricing policies and seasonal surcharges
        group.MapGet("/policies", () =>
        {
            var policies = new[]
            {
                new PricingPolicyDto("WEEKEND_SURCHARGE", "Weekend Surge Pricing", "Saturday and Sunday apply a +15% daily rate surcharge."),
                new PricingPolicyDto("HOLIDAY_SURCHARGE", "Tet & National Holidays", "Major national holidays apply a +30% seasonal multiplier."),
                new PricingPolicyDto("INSURANCE_BASIC", "Basic Collision Damage Waiver", "Optional 120,000 VND/day coverage reducing deductible to zero."),
                new PricingPolicyDto("DEPOSIT_HOLD", "Security Deposit Policy", "5,000,000 VND authorization hold released 48h after return handover.")
            };

            return Results.Ok(policies);
        })
        .WithName("GetPricingPolicies")
        .WithSummary("List platform pricing and surcharge policies")
        .WithDescription("Retrieves the active global pricing policies, surcharge rules, and insurance option details.")
        .Produces<PricingPolicyDto[]>(StatusCodes.Status200OK);

        return app;
    }
}

public sealed record VehiclePricingDto(
    Guid VehicleId,
    decimal BaseDailyRate,
    decimal WeekendDailyRate,
    decimal SecurityDeposit,
    string Currency,
    decimal OverKmSurchargePerKm,
    int DailyKmLimit);

public sealed record EstimatePriceRequest(
    Guid VehicleId,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    bool IncludeInsurance);

public sealed record PriceEstimateDto(
    Guid VehicleId,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    int TotalDays,
    decimal BaseRent,
    decimal WeekendSurcharge,
    decimal InsuranceFee,
    decimal SecurityDeposit,
    decimal TotalEstimatedCost,
    string Currency);

public sealed record PricingPolicyDto(
    string Code,
    string Name,
    string Description);
