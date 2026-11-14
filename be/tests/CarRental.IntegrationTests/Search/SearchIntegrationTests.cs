using System.Net;
using System.Net.Http.Json;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;
using CarRental.IntegrationTests.Fixtures;
using CarRental.Modules.Fleet.Contracts.Events;
using CarRental.Modules.Pricing.Contracts.Events;
using CarRental.Modules.Search.Contracts.Dtos;
using CarRental.Modules.Search.Data;
using CarRental.Modules.Search.Entities;
using CarRental.Modules.Search.EventHandlers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Xunit;

namespace CarRental.IntegrationTests.Search;

public class SearchIntegrationTests : IntegrationTestBase
{
    private static readonly GeoPoint HanoiCenter = GeoPoint.Create(21.0285, 105.8542).Value;
    private static readonly GeoPoint HcmcCenter = GeoPoint.Create(10.7769, 106.7009).Value;

    public SearchIntegrationTests(CarRentalApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedVehicleSearchDocumentAsync(
        string brand,
        string model,
        int seats,
        string transmission,
        decimal? priceFrom,
        GeoPoint location,
        string address,
        bool isPublished = true,
        decimal? rating = 4.8m,
        int reviews = 15)
    {
        var vehicleId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();

        var doc = VehicleSearchDocument.Create(vehicleId);
        if (isPublished)
        {
            doc.ApplyPublished(brand, model, seats, transmission, location, address, now);
        }

        if (priceFrom.HasValue)
        {
            doc.ApplyPrice(priceFrom.Value, now);
        }

        if (rating.HasValue)
        {
            doc.ApplyRating(rating.Value, reviews, now);
        }

        db.VehicleSearchDocuments.Add(doc);
        await db.SaveChangesAsync();

        return vehicleId;
    }

    [Fact]
    public async Task SearchVehicles_WithBrandFilter_ShouldReturnOnlyMatchingVehicles()
    {
        await SeedVehicleSearchDocumentAsync("Toyota", "Vios", 5, "Automatic", 600_000m, HanoiCenter, "Hoan Kiem, Hanoi");
        await SeedVehicleSearchDocumentAsync("Honda", "City", 5, "Automatic", 650_000m, HanoiCenter, "Cau Giay, Hanoi");

        var response = await Client.GetAsync("/api/search/vehicles?brand=Toyota");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedVehicleSearchResultDto>();
        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items.First().Brand.Should().Be("Toyota");
        result.Items.First().Model.Should().Be("Vios");
    }

    [Fact]
    public async Task SearchVehicles_WithPriceRangeFilter_ShouldFilterAccurately()
    {
        await SeedVehicleSearchDocumentAsync("Hyundai", "Accent", 5, "Manual", 500_000m, HanoiCenter, "Hanoi");
        await SeedVehicleSearchDocumentAsync("Kia", "K3", 5, "Automatic", 750_000m, HanoiCenter, "Hanoi");
        await SeedVehicleSearchDocumentAsync("Mercedes", "C200", 5, "Automatic", 1_800_000m, HanoiCenter, "Hanoi");

        var response = await Client.GetAsync("/api/search/vehicles?minPrice=600000&maxPrice=1000000");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedVehicleSearchResultDto>();
        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(1);
        result.Items.First().Model.Should().Be("K3");
    }

    [Fact]
    public async Task SearchVehicles_UnpublishedOrUnpriced_ShouldNotBeVisible()
    {
        // Published and priced
        await SeedVehicleSearchDocumentAsync("Mazda", "3", 5, "Automatic", 700_000m, HanoiCenter, "Hanoi", isPublished: true);
        // Unpublished
        await SeedVehicleSearchDocumentAsync("Mazda", "6", 5, "Automatic", 900_000m, HanoiCenter, "Hanoi", isPublished: false);
        // Published but unpriced
        await SeedVehicleSearchDocumentAsync("Mazda", "CX-5", 5, "Automatic", null, HanoiCenter, "Hanoi", isPublished: true);

        var response = await Client.GetAsync("/api/search/vehicles?brand=Mazda");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedVehicleSearchResultDto>();
        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(1);
        result.Items.First().Model.Should().Be("3");
    }

    [Fact]
    public async Task SearchVehicles_WithGeospatialRadius_ShouldFilterByDistance()
    {
        // Vehicle in Hanoi
        await SeedVehicleSearchDocumentAsync("VinFast", "VF8", 5, "Automatic", 1_200_000m, HanoiCenter, "Hoan Kiem, Hanoi");
        // Vehicle in Ho Chi Minh City (~1100 km away)
        await SeedVehicleSearchDocumentAsync("VinFast", "VF9", 7, "Automatic", 1_800_000m, HcmcCenter, "District 1, HCMC");

        // Search within 30 km radius of Hanoi center
        var url = $"/api/search/vehicles?latitude={HanoiCenter.Latitude}&longitude={HanoiCenter.Longitude}&radiusKm=30";
        var response = await Client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedVehicleSearchResultDto>();
        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(1);
        result.Items.First().Model.Should().Be("VF8");
        result.Items.First().DistanceKm.Should().BeLessThan(5);
    }

    [Fact]
    public async Task EventDriven_FleetVehiclePublished_ShouldProjectToSearchReadModel()
    {
        var eventId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // 1. Dispatch VehiclePublished integration event via Wolverine Bus
        using (var scope = CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.PublishAsync(new VehiclePublished(
                eventId,
                now,
                vehicleId,
                ownerId,
                "Subaru",
                "Forester",
                5,
                "Automatic",
                HanoiCenter,
                "Tay Ho, Hanoi"));

            // Publish pricing to make the vehicle visible
            await bus.PublishAsync(new PricingUpdated(
                Guid.NewGuid(),
                now,
                vehicleId,
                850_000m));
        }

        // Allow Wolverine transactional outbox / local queue time to commit and process both events.
        await Task.Delay(2000);

        // 2. Query search endpoint to verify the event projection is live in search catalog
        var response = await Client.GetAsync("/api/search/vehicles?brand=Subaru");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedVehicleSearchResultDto>();
        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(1);
        result.Items.First().Brand.Should().Be("Subaru");
        result.Items.First().Model.Should().Be("Forester");
        result.Items.First().PriceFrom.Should().Be(850_000m);
    }
}
