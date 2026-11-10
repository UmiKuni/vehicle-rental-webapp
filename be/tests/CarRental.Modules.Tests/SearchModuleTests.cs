using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Fleet.Contracts.Events;
using CarRental.Modules.Pricing.Contracts.Events;
using CarRental.Modules.Reviews.Contracts.Events;
using CarRental.Modules.Search.Contracts.Dtos;
using CarRental.Modules.Search.Entities;
using CarRental.Modules.Search.EventHandlers;
using CarRental.Modules.Search.Services;
using FluentAssertions;
using Xunit;

namespace CarRental.Modules.Tests;

public class SearchModuleTests
{
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly GeoPoint HanoiLocation = GeoPoint.Create(21.0285, 105.8542).Value;

    [Fact]
    public void ApplyPublished_ShouldSetProperties_AndMarkAsPublished()
    {
        var doc = VehicleSearchDocument.Create(VehicleId);

        var applied = doc.ApplyPublished("Toyota", "Vios", 5, "Automatic", HanoiLocation, "Hoan Kiem, Hanoi", Now);

        applied.Should().BeTrue();
        doc.IsPublished.Should().BeTrue();
        doc.Brand.Should().Be("Toyota");
        doc.Model.Should().Be("Vios");
        doc.Seats.Should().Be(5);
        doc.Transmission.Should().Be("Automatic");
        doc.Location.Should().Be(HanoiLocation);
        doc.Address.Should().Be("Hoan Kiem, Hanoi");
        doc.LastFleetEventAt.Should().Be(Now);
    }

    [Fact]
    public void ApplyPublished_WithStaleEvent_ShouldBeIgnored()
    {
        var doc = VehicleSearchDocument.Create(VehicleId);
        doc.ApplyPublished("Toyota", "Vios", 5, "Automatic", HanoiLocation, "Hoan Kiem, Hanoi", Now);

        var staleResult = doc.ApplyPublished("Honda", "City", 5, "Manual", HanoiLocation, "Old Address", Now.AddMinutes(-5));

        staleResult.Should().BeFalse();
        doc.Brand.Should().Be("Toyota");
        doc.Model.Should().Be("Vios");
        doc.Transmission.Should().Be("Automatic");
    }

    [Fact]
    public void ApplyPublished_WithNewerEvent_ShouldOverwriteExistingData()
    {
        var doc = VehicleSearchDocument.Create(VehicleId);
        doc.ApplyPublished("Toyota", "Vios", 5, "Automatic", HanoiLocation, "Old Address", Now);

        var newerTime = Now.AddHours(1);
        var newerLocation = GeoPoint.Create(10.7769, 106.7009).Value;
        var updated = doc.ApplyPublished("Toyota", "Camry", 5, "Automatic", newerLocation, "District 1, HCMC", newerTime);

        updated.Should().BeTrue();
        doc.Model.Should().Be("Camry");
        doc.Location.Should().Be(newerLocation);
        doc.Address.Should().Be("District 1, HCMC");
        doc.LastFleetEventAt.Should().Be(newerTime);
    }

    [Fact]
    public void ApplyUnpublished_ShouldMarkAsUnpublished()
    {
        var doc = VehicleSearchDocument.Create(VehicleId);
        doc.ApplyPublished("Toyota", "Vios", 5, "Automatic", HanoiLocation, "Hoan Kiem, Hanoi", Now);

        var unpublishResult = doc.ApplyUnpublished(Now.AddMinutes(10));

        unpublishResult.Should().BeTrue();
        doc.IsPublished.Should().BeFalse();
        doc.LastFleetEventAt.Should().Be(Now.AddMinutes(10));
    }

    [Fact]
    public void ApplyUnpublished_WithStaleEvent_ShouldBeIgnored()
    {
        var doc = VehicleSearchDocument.Create(VehicleId);
        doc.ApplyPublished("Toyota", "Vios", 5, "Automatic", HanoiLocation, "Hoan Kiem, Hanoi", Now);

        var staleResult = doc.ApplyUnpublished(Now.AddMinutes(-10));

        staleResult.Should().BeFalse();
        doc.IsPublished.Should().BeTrue();
    }

    [Fact]
    public void ApplyPrice_ShouldUpdatePriceFrom()
    {
        var doc = VehicleSearchDocument.Create(VehicleId);

        var applied = doc.ApplyPrice(750_000m, Now);

        applied.Should().BeTrue();
        doc.PriceFrom.Should().Be(750_000m);
        doc.LastPricingEventAt.Should().Be(Now);
    }

    [Fact]
    public void ApplyPrice_WithStaleEvent_ShouldBeIgnored()
    {
        var doc = VehicleSearchDocument.Create(VehicleId);
        doc.ApplyPrice(750_000m, Now);

        var stale = doc.ApplyPrice(600_000m, Now.AddMinutes(-10));

        stale.Should().BeFalse();
        doc.PriceFrom.Should().Be(750_000m);
    }

    [Fact]
    public void ApplyRating_ShouldUpdateRatingAndReviews()
    {
        var doc = VehicleSearchDocument.Create(VehicleId);

        var applied = doc.ApplyRating(4.85m, 24, Now);

        applied.Should().BeTrue();
        doc.AverageRating.Should().Be(4.85m);
        doc.TotalReviews.Should().Be(24);
        doc.LastRatingEventAt.Should().Be(Now);
    }

    [Fact]
    public void ApplyRating_WithStaleEvent_ShouldBeIgnored()
    {
        var doc = VehicleSearchDocument.Create(VehicleId);
        doc.ApplyRating(4.85m, 24, Now);

        var stale = doc.ApplyRating(3.5m, 5, Now.AddMinutes(-15));

        stale.Should().BeFalse();
        doc.AverageRating.Should().Be(4.85m);
        doc.TotalReviews.Should().Be(24);
    }

    [Fact]
    public void IsVisible_ShouldBeTrue_OnlyWhenPublishedAndPriceFromNotNull()
    {
        var doc = VehicleSearchDocument.Create(VehicleId);
        doc.ApplyPublished("Mazda", "CX-5", 5, "Automatic", HanoiLocation, "Ba Dinh, Hanoi", Now);
        doc.ApplyPrice(900_000m, Now);

        doc.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void IsVisible_ShouldBeFalse_WhenPublishedButPriceIsNull()
    {
        var doc = VehicleSearchDocument.Create(VehicleId);
        doc.ApplyPublished("Mazda", "CX-5", 5, "Automatic", HanoiLocation, "Ba Dinh, Hanoi", Now);

        doc.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void IsVisible_ShouldBeFalse_WhenUnpublishedEvenIfPriceExists()
    {
        var doc = VehicleSearchDocument.Create(VehicleId);
        doc.ApplyPublished("Mazda", "CX-5", 5, "Automatic", HanoiLocation, "Ba Dinh, Hanoi", Now);
        doc.ApplyPrice(900_000m, Now);
        doc.ApplyUnpublished(Now.AddMinutes(5));

        doc.IsVisible.Should().BeFalse();
    }

    // =========================================================================
    // SearchApiService Input Validation Tests
    // =========================================================================

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task SearchVehicles_WithInvalidPaging_ShouldFail(int page, int pageSize)
    {
        var service = new SearchApiService(null!);
        var request = new VehicleSearchRequest(Page: page, PageSize: pageSize);

        var result = await service.SearchVehiclesAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("search.invalid_paging");
    }

    [Fact]
    public async Task SearchVehicles_WithNegativeMinPrice_ShouldFail()
    {
        var service = new SearchApiService(null!);
        var request = new VehicleSearchRequest(MinPrice: -100);

        var result = await service.SearchVehiclesAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("search.invalid_price_range");
    }

    [Fact]
    public async Task SearchVehicles_WithMinPriceGreaterThanMaxPrice_ShouldFail()
    {
        var service = new SearchApiService(null!);
        var request = new VehicleSearchRequest(MinPrice: 1_000_000, MaxPrice: 500_000);

        var result = await service.SearchVehiclesAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("search.invalid_price_range");
    }

    [Fact]
    public async Task SearchVehicles_WithIncompleteLocation_ShouldFail()
    {
        var service = new SearchApiService(null!);

        // Latitude provided without longitude or radius
        var req1 = new VehicleSearchRequest(Latitude: 10.7769);
        var res1 = await service.SearchVehiclesAsync(req1);
        res1.IsSuccess.Should().BeFalse();
        res1.Error.Code.Should().Be("search.invalid_location");

        // Coordinates without radius
        var req2 = new VehicleSearchRequest(Latitude: 10.7769, Longitude: 106.7009, RadiusKm: null);
        var res2 = await service.SearchVehiclesAsync(req2);
        res2.IsSuccess.Should().BeFalse();
        res2.Error.Code.Should().Be("search.invalid_location");

        // Coordinates with zero radius
        var req3 = new VehicleSearchRequest(Latitude: 10.7769, Longitude: 106.7009, RadiusKm: 0);
        var res3 = await service.SearchVehiclesAsync(req3);
        res3.IsSuccess.Should().BeFalse();
        res3.Error.Code.Should().Be("search.invalid_location");
    }

    [Fact]
    public async Task SearchVehicles_WithOutOfRangeCoordinates_ShouldFail()
    {
        var service = new SearchApiService(null!);
        var request = new VehicleSearchRequest(Latitude: 95.0, Longitude: 106.7009, RadiusKm: 10);

        var result = await service.SearchVehiclesAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("geo.invalid_latitude");
    }

    // =========================================================================
    // Wolverine Consumer Execution Tests
    // =========================================================================

    [Fact]
    public async Task SearchConsumers_ShouldExecuteWithoutExceptions()
    {
        var pubConsumer = new VehiclePublishedConsumer();
        var unpubConsumer = new VehicleUnpublishedConsumer();
        var priceConsumer = new PricingUpdatedConsumer();
        var ratingConsumer = new RatingSummaryUpdatedConsumer();

        await pubConsumer.Consume(new VehiclePublished(Guid.NewGuid(), "Honda", "City", 5, "Auto", HanoiLocation, "Hanoi", DateTimeOffset.UtcNow));
        await unpubConsumer.Consume(new VehicleUnpublished(Guid.NewGuid(), DateTimeOffset.UtcNow));
        await priceConsumer.Consume(new PricingUpdated(Guid.NewGuid(), 500_000m, DateTimeOffset.UtcNow));
        await ratingConsumer.Consume(new RatingSummaryUpdated(Guid.NewGuid(), 4.9m, 12, DateTimeOffset.UtcNow));

        // If no unhandled exception occurred, consumers completed successfully
        Assert.True(true);
    }
}
