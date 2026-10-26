using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Search.Entities;
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
}
