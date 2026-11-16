using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Availability.Data;
using CarRental.Modules.Availability.Data.Repositories;
using CarRental.Modules.Availability.Entities;
using CarRental.Modules.Availability.EventHandlers;
using CarRental.Modules.Bookings.Contracts.Events;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CarRental.Modules.Tests.Availability;

public class AvailabilityEventHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AvailabilityEventHandlers_ShouldReleaseBookingHoldsOnCancelledRejectedAndExpired()
    {
        var clockMock = new Mock<TimeProvider>();
        clockMock.Setup(x => x.GetUtcNow()).Returns(Now);

        var dbContextMock = new Mock<AvailabilityDbContext>(new DbContextOptionsBuilder<AvailabilityDbContext>().Options);
        dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new AvailabilityEventHandlers();

        var cancelledBookingId = Guid.NewGuid();
        var rejectedBookingId = Guid.NewGuid();
        var expiredBookingId = Guid.NewGuid();

        var calendar = VehicleCalendar.Create(Guid.NewGuid(), Now);
        calendar.PlaceHold(cancelledBookingId, DateTimeRange.Create(Now.AddDays(1), Now.AddDays(2)).Value, TimeSpan.FromHours(2), Now);
        calendar.PlaceHold(rejectedBookingId, DateTimeRange.Create(Now.AddDays(3), Now.AddDays(4)).Value, TimeSpan.FromHours(2), Now);
        calendar.PlaceHold(expiredBookingId, DateTimeRange.Create(Now.AddDays(5), Now.AddDays(6)).Value, TimeSpan.FromHours(2), Now);

        var repoMock = new Mock<IVehicleCalendarRepository>();
        repoMock.Setup(x => x.GetByBookingIdAsync(
                It.Is<Guid>(id => calendar.Entries.Any(e => e.BookingId == id)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(calendar);

        await handler.Handle(
            new BookingCancelled(Guid.NewGuid(), Now, cancelledBookingId, Guid.NewGuid(), new Money(0, "VND"), new Money(500_000, "VND"), "Customer cancelled"),
            repoMock.Object,
            dbContextMock.Object,
            clockMock.Object,
            CancellationToken.None);

        calendar.Entries.Should().NotContain(e => e.BookingId == cancelledBookingId);
        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        await handler.Handle(
            new BookingRejected(Guid.NewGuid(), Now, rejectedBookingId, Guid.NewGuid(), "Owner declined"),
            repoMock.Object,
            dbContextMock.Object,
            clockMock.Object,
            CancellationToken.None);

        calendar.Entries.Should().NotContain(e => e.BookingId == rejectedBookingId);
        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));

        await handler.Handle(
            new BookingExpired(Guid.NewGuid(), Now, expiredBookingId, "Payment timeout"),
            repoMock.Object,
            dbContextMock.Object,
            clockMock.Object,
            CancellationToken.None);

        calendar.Entries.Should().BeEmpty();
        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
