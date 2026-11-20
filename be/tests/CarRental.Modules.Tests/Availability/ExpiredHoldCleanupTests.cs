using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Availability.Data;
using CarRental.Modules.Availability.Data.Repositories;
using CarRental.Modules.Availability.Entities;
using CarRental.Modules.Availability.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CarRental.Modules.Tests.Availability;

public class ExpiredHoldCleanupTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CleanupExpiredHoldsOnceAsync_ShouldExpirePastHoldsAndSaveChanges()
    {
        var currentTime = Now.AddHours(2);
        var clockMock = new Mock<TimeProvider>();
        clockMock.Setup(x => x.GetUtcNow()).Returns(() => currentTime);

        var calendar = VehicleCalendar.Create(Guid.NewGuid(), Now);
        var expiredBooking = Guid.NewGuid();
        var activeBooking = Guid.NewGuid();
        calendar.PlaceHold(expiredBooking, DateTimeRange.Create(Now.AddDays(1), Now.AddDays(2)).Value, TimeSpan.FromHours(1), Now);
        calendar.PlaceHold(activeBooking, DateTimeRange.Create(Now.AddDays(4), Now.AddDays(5)).Value, TimeSpan.FromHours(6), Now);

        var repoMock = new Mock<IVehicleCalendarRepository>();
        repoMock.Setup(x => x.GetCalendarsWithExpiredHoldsAsync(currentTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync([calendar]);

        var dbContextMock = new Mock<AvailabilityDbContext>(new DbContextOptionsBuilder<AvailabilityDbContext>().Options);
        dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var services = new ServiceCollection();
        services.AddScoped<IVehicleCalendarRepository>(_ => repoMock.Object);
        services.AddScoped<AvailabilityDbContext>(_ => dbContextMock.Object);
        using var provider = services.BuildServiceProvider();

        var cleanupService = new ExpiredHoldCleanupService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            clockMock.Object,
            NullLogger<ExpiredHoldCleanupService>.Instance);

        var cleanedCount = await cleanupService.CleanupExpiredHoldsOnceAsync(CancellationToken.None);

        cleanedCount.Should().Be(1);
        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        calendar.Entries.Should().ContainSingle(e => e.BookingId == activeBooking);
    }
}
