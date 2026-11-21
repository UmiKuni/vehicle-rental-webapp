using System.Net;
using System.Net.Http.Json;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.IntegrationTests.Fixtures;
using CarRental.Modules.Fleet.Contracts.Enums;
using CarRental.Modules.Fleet.Data;
using CarRental.Modules.Fleet.Entities;
using CarRental.Modules.Pricing.Contracts.Dtos;
using CarRental.Modules.Pricing.Data;
using CarRental.Modules.Search.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarRental.IntegrationTests.Pricing;

public class PricingIntegrationTests : IntegrationTestBase
{
    public PricingIntegrationTests(CarRentalApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedVehicleAsync(Guid ownerId, string plateNumber)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        var brand = new VehicleBrand(Guid.NewGuid(), "Toyota");
        var model = new VehicleModel(Guid.NewGuid(), brand.Id, "Camry");
        var vehicle = Vehicle.Create(
            ownerId,
            plateNumber,
            brand.Id,
            model.Id,
            TransmissionType.Auto,
            5,
            new GeoPoint(10.7769, 106.7009),
            "District 1, HCMC").Value;

        db.VehicleBrands.Add(brand);
        db.VehicleModels.Add(model);
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();

        return vehicle.Id.Value;
    }

    [Fact]
    public async Task ConfigurePricing_AsVehicleOwner_ShouldCreateAndUpdatePricingPlanAndCreateVersionSnapshot()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_pricing@example.com",
            "0901112233",
            taxCode: "0102030406");
        var vehicleId = await SeedVehicleAsync(ownerId, "51A-555.01");

        // 1. Initial configure
        var createRequest = new ConfigurePricingDto(
            HourlyRate: null,
            WeekdayRate: new Money(600_000, "VND"),
            WeekendRate: new Money(800_000, "VND"),
            DepositAmount: new Money(5_000_000, "VND"),
            KmLimit: 250,
            OverKmFee: new Money(5_000, "VND"),
            CleaningFee: new Money(100_000, "VND"),
            OwnerId: Guid.Empty);

        var createResponse = await ownerClient.PutAsJsonAsync($"/api/pricing/vehicles/{vehicleId}", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createdPlan = await createResponse.Content.ReadFromJsonAsync<PricingPlanDto>();
        createdPlan.Should().NotBeNull();
        createdPlan!.VehicleId.Should().Be(vehicleId);
        createdPlan.WeekdayRate.Amount.Should().Be(600_000);
        createdPlan.WeekendRate.Amount.Should().Be(800_000);
        createdPlan.FeeRules.Should().HaveCount(2);

        // 2. Update pricing: modify rates and remove Cleaning fee
        var updateRequest = new ConfigurePricingDto(
            HourlyRate: new Money(100_000, "VND"),
            WeekdayRate: new Money(650_000, "VND"),
            WeekendRate: new Money(900_000, "VND"),
            DepositAmount: new Money(5_000_000, "VND"),
            KmLimit: 300,
            OverKmFee: new Money(6_000, "VND"),
            CleaningFee: null,
            OwnerId: Guid.Empty);

        var updateResponse = await ownerClient.PutAsJsonAsync($"/api/pricing/vehicles/{vehicleId}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedPlan = await updateResponse.Content.ReadFromJsonAsync<PricingPlanDto>();
        updatedPlan.Should().NotBeNull();
        updatedPlan!.WeekdayRate.Amount.Should().Be(650_000);
        updatedPlan.WeekendRate.Amount.Should().Be(900_000);
        updatedPlan.HourlyRate!.Amount.Should().Be(100_000);
        updatedPlan.KmLimit.Should().Be(300);
        updatedPlan.FeeRules.Should().ContainSingle(r => r.Type == "OverKm" && r.Amount.Amount == 6_000);

        // Verify persisted state & version snapshots in PostgreSQL
        using (var scope = CreateScope())
        {
            var pricingDb = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
            var versionsCount = await pricingDb.PricingPlanVersions
                .CountAsync(v => v.VehicleId == vehicleId);
            versionsCount.Should().Be(2);
        }

        // 3. GET pricing endpoint returns updated plan
        var getResponse = await Client.GetAsync($"/api/pricing/vehicles/{vehicleId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedPlan = await getResponse.Content.ReadFromJsonAsync<PricingPlanDto>();
        fetchedPlan!.WeekdayRate.Amount.Should().Be(650_000);
        fetchedPlan.FeeRules.Should().ContainSingle(r => r.Type == "OverKm");

        // 4. POST quote endpoint calculates quote using updated plan (48 started hours * 100,000 VND hourly rate)
        var quoteRequest = new PricingQuoteRequest(
            vehicleId,
            new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero), // Monday
            new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero)); // Wednesday (48h)
        var quoteResponse = await Client.PostAsJsonAsync("/api/pricing/quotes", quoteRequest);
        quoteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var quote = await quoteResponse.Content.ReadFromJsonAsync<QuoteDto>();
        quote.Should().NotBeNull();
        quote!.Total.Amount.Should().Be(4_800_000);
        quote.DepositAmount.Amount.Should().Be(5_000_000);
        quote.OverKmFeePerKm!.Amount.Should().Be(6_000);

        // 5. Verify PricingUpdated integration event propagated to Search read model
        decimal? projectedPriceFrom = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var scope = CreateScope();
            var searchDb = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
            var doc = await searchDb.VehicleSearchDocuments
                .AsNoTracking()
                .SingleOrDefaultAsync(d => d.Id == vehicleId);
            if (doc?.PriceFrom == 650_000m)
            {
                projectedPriceFrom = doc.PriceFrom;
                break;
            }

            await Task.Delay(150);
        }

        projectedPriceFrom.Should().Be(650_000m);
    }

    [Fact]
    public async Task ConfigurePricing_AsDifferentOwner_ShouldReturn403Forbidden()
    {
        var (_, ownerAId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_pricing_a@example.com",
            "0902223344",
            taxCode: "0102030407");
        var (ownerBClient, _) = await CreateAuthenticatedOwnerClientAsync(
            "owner_pricing_b@example.com",
            "0903334455",
            taxCode: "0102030408");
        var vehicleId = await SeedVehicleAsync(ownerAId, "51A-555.02");

        var request = new ConfigurePricingDto(
            HourlyRate: null,
            WeekdayRate: new Money(600_000, "VND"),
            WeekendRate: new Money(800_000, "VND"),
            DepositAmount: new Money(5_000_000, "VND"),
            KmLimit: 250,
            OverKmFee: null,
            CleaningFee: null,
            OwnerId: ownerAId);

        var response = await ownerBClient.PutAsJsonAsync($"/api/pricing/vehicles/{vehicleId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
