using System.Reflection;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Fleet.Contracts;
using CarRental.Modules.Fleet.Contracts.Dtos;
using CarRental.Modules.Pricing.Contracts.Dtos;
using CarRental.Modules.Pricing.Data;
using CarRental.Modules.Pricing.Entities;
using CarRental.Modules.Pricing.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CarRental.Modules.Tests.Pricing;

public class PricingApiServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PricingErrors_ShouldHaveUniqueConformingCodesAndExpectedTypes()
    {
        var errorsType = typeof(PricingPlan).Assembly.GetType("CarRental.Modules.Pricing.Errors.PricingErrors");
        errorsType.Should().NotBeNull();

        var fieldErrors = errorsType!
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Error))
            .Select(f => (Name: f.Name, Error: (Error)f.GetValue(null)!));

        var propertyErrors = errorsType
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(Error))
            .Select(p => (Name: p.Name, Error: (Error)p.GetValue(null)!));

        var allErrors = fieldErrors.Concat(propertyErrors).ToList();
        allErrors.Should().NotBeEmpty();

        foreach (var (name, error) in allErrors)
        {
            error.Code.Should().MatchRegex("^pricing\\.[a-z_]+$", $"Error '{name}' must use pricing.* snake_case naming.");
        }

        allErrors.Select(e => e.Error.Code).Should().OnlyHaveUniqueItems();

        var ownerRequired = allErrors.Single(e => e.Name == "OwnerRequired").Error;
        ownerRequired.Code.Should().Be("pricing.owner_required");
        ownerRequired.Type.Should().Be(ErrorType.Forbidden);

        var notFound = allErrors.Single(e => e.Name == "NotFound").Error;
        notFound.Code.Should().Be("pricing.not_found");
        notFound.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task ConfigurePricingAsync_WithEmptyVehicleId_ShouldFailWithInvalidVehicle()
    {
        var fleetMock = CreateFleetMockWithFailure(Error.NotFound("fleet.not_found", "Not found"));
        var outboxMock = new Mock<IEventOutbox>();
        var service = CreatePricingService(fleetMock, outboxMock);
        var dto = CreateConfigureDto(Guid.NewGuid());

        var result = await service.ConfigurePricingAsync(Guid.Empty, dto);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("pricing.invalid_vehicle");
        result.Error.Type.Should().Be(ErrorType.Validation);
        fleetMock.Verify(x => x.GetVehicleSummaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        outboxMock.Verify(x => x.PublishRangeAsync(It.IsAny<IEnumerable<IIntegrationEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfigurePricingAsync_WhenFleetReturnsFailure_ShouldPropagateError()
    {
        var fleetError = Error.NotFound("fleet.vehicle_not_found", "Vehicle was not found.");
        var fleetMock = CreateFleetMockWithFailure(fleetError);
        var outboxMock = new Mock<IEventOutbox>();
        var service = CreatePricingService(fleetMock, outboxMock);
        var dto = CreateConfigureDto(Guid.NewGuid());

        var result = await service.ConfigurePricingAsync(Guid.NewGuid(), dto);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(fleetError);
        outboxMock.Verify(x => x.PublishRangeAsync(It.IsAny<IEnumerable<IIntegrationEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfigurePricingAsync_WhenCallerIsNotOwner_ShouldFailWithForbidden()
    {
        var actualOwnerId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        var fleetMock = CreateFleetMockForOwner(actualOwnerId);
        var outboxMock = new Mock<IEventOutbox>();
        var service = CreatePricingService(fleetMock, outboxMock);
        var dto = CreateConfigureDto(differentUserId);

        var result = await service.ConfigurePricingAsync(vehicleId, dto);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("pricing.owner_required");
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        outboxMock.Verify(x => x.PublishRangeAsync(It.IsAny<IEnumerable<IIntegrationEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void DateTimeRange_Create_WithInvalidRange_ShouldFail()
    {
        var sameTimeResult = DateTimeRange.Create(Now, Now);
        sameTimeResult.IsFailure.Should().BeTrue();
        sameTimeResult.Error.Code.Should().Be("datetime_range.invalid_range");

        var reversedResult = DateTimeRange.Create(Now.AddHours(2), Now);
        reversedResult.IsFailure.Should().BeTrue();
        reversedResult.Error.Code.Should().Be("datetime_range.invalid_range");
    }

    private static ConfigurePricingDto CreateConfigureDto(Guid ownerId) =>
        new(
            new Money(100_000, "VND"),
            new Money(500_000, "VND"),
            new Money(700_000, "VND"),
            new Money(5_000_000, "VND"),
            300,
            new Money(5_000, "VND"),
            new Money(100_000, "VND"),
            ownerId);

    private static Mock<IFleetApi> CreateFleetMockForOwner(Guid ownerId)
    {
        var mock = new Mock<IFleetApi>();
        mock.Setup(x => x.GetVehicleSummaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid vehicleId, CancellationToken _) => Result<VehicleSummaryDto>.Success(
                new VehicleSummaryDto(
                    vehicleId,
                    ownerId,
                    "Published",
                    Guid.NewGuid(),
                    "Toyota",
                    Guid.NewGuid(),
                    "Camry",
                    "51A-12345",
                    "Automatic",
                    5,
                    null,
                    GeoPoint.Create(10.7769, 106.7009).Value,
                    "Ho Chi Minh City")));
        return mock;
    }

    private static Mock<IFleetApi> CreateFleetMockWithFailure(Error error)
    {
        var mock = new Mock<IFleetApi>();
        mock.Setup(x => x.GetVehicleSummaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<VehicleSummaryDto>.Failure(error));
        return mock;
    }

    private static PricingApiService CreatePricingService(
        Mock<IFleetApi> fleetMock,
        Mock<IEventOutbox> outboxMock)
    {
        var dbContext = new PricingDbContext(new DbContextOptionsBuilder<PricingDbContext>().Options);
        var clockMock = new Mock<TimeProvider>();
        clockMock.Setup(x => x.GetUtcNow()).Returns(Now);

        return new PricingApiService(
            dbContext,
            fleetMock.Object,
            outboxMock.Object,
            clockMock.Object);
    }
}
