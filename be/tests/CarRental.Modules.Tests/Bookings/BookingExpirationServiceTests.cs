using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Availability.Contracts;
using CarRental.Modules.Bookings.Contracts.Enums;
using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Bookings.Data;
using CarRental.Modules.Bookings.Data.Repositories;
using CarRental.Modules.Bookings.Entities;
using CarRental.Modules.Bookings.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CarRental.Modules.Tests.Bookings;

public class BookingExpirationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExpireOnceAsync_ShouldExpireOverduePendingAndConfirmedBookingsReleaseHoldsAndPublishEvents()
    {
        var pendingId = BookingId.New();
        var pendingBooking = CreateBooking(pendingId, Now.AddHours(-13));

        var confirmedId = BookingId.New();
        var confirmedBooking = CreateBooking(confirmedId, Now.AddHours(-3));
        confirmedBooking.Approve(confirmedBooking.OwnerId, Now.AddHours(-2).AddMinutes(-5));

        var bookings = new Dictionary<Guid, Booking>
        {
            [pendingId.Value] = pendingBooking,
            [confirmedId.Value] = confirmedBooking
        };

        var dbContextMock = new Mock<BookingDbContext>(new DbContextOptionsBuilder<BookingDbContext>().Options);
        dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var repoMock = new Mock<IBookingRepository>();
        repoMock.Setup(x => x.GetExpiredPendingIdsAsync(Now.AddHours(-12), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pendingId]);
        repoMock.Setup(x => x.GetExpiredConfirmedIdsAsync(Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync([confirmedId]);
        repoMock.Setup(x => x.GetByIdAsync(It.IsAny<BookingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BookingId id, CancellationToken _) => bookings.GetValueOrDefault(id.Value));

        var availabilityMock = new Mock<IAvailabilityApi>();
        availabilityMock.Setup(x => x.ReleaseHoldAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var outboxMock = new Mock<IEventOutbox>();
        outboxMock.Setup(x => x.PublishAsync(It.IsAny<BookingExpired>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateExpirationService(repoMock.Object, dbContextMock.Object, availabilityMock.Object, outboxMock.Object);

        var count = await service.ExpireOnceAsync();

        count.Should().Be(2);
        pendingBooking.Status.Should().Be(BookingStatus.Expired);
        confirmedBooking.Status.Should().Be(BookingStatus.Expired);
        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        availabilityMock.Verify(x => x.ReleaseHoldAsync(pendingBooking.VehicleId, pendingId.Value, It.IsAny<CancellationToken>()), Times.Once);
        availabilityMock.Verify(x => x.ReleaseHoldAsync(confirmedBooking.VehicleId, confirmedId.Value, It.IsAny<CancellationToken>()), Times.Once);
        outboxMock.Verify(x => x.PublishAsync(It.Is<BookingExpired>(e => e.BookingId == pendingId.Value), It.IsAny<CancellationToken>()), Times.Once);
        outboxMock.Verify(x => x.PublishAsync(It.Is<BookingExpired>(e => e.BookingId == confirmedId.Value), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpireOnceAsync_WhenNoneExpired_ShouldReturnZeroWithoutSaving()
    {
        var dbContextMock = new Mock<BookingDbContext>(new DbContextOptionsBuilder<BookingDbContext>().Options);
        var repoMock = new Mock<IBookingRepository>();
        repoMock.Setup(x => x.GetExpiredPendingIdsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        repoMock.Setup(x => x.GetExpiredConfirmedIdsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var availabilityMock = new Mock<IAvailabilityApi>();
        var outboxMock = new Mock<IEventOutbox>();

        var service = CreateExpirationService(repoMock.Object, dbContextMock.Object, availabilityMock.Object, outboxMock.Object);

        var count = await service.ExpireOnceAsync();

        count.Should().Be(0);
        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        availabilityMock.Verify(x => x.ReleaseHoldAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Booking CreateBooking(BookingId id, DateTimeOffset createdAt)
    {
        var lines = new List<BookingLineItem>
        {
            new(Guid.NewGuid(), id, BookingLineItemType.Rent, Money.Of(1_500_000)),
            new(Guid.NewGuid(), id, BookingLineItemType.Deposit, Money.Of(5_000_000))
        };
        var policy = new CancellationPolicySnapshot(id, "[]");
        var period = new DateTimeRange(createdAt.AddDays(3), createdAt.AddDays(5));

        return Booking.Request(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            period,
            "HCMC",
            lines,
            policy,
            createdAt,
            id).Value;
    }

    private static BookingExpirationService CreateExpirationService(
        IBookingRepository repository,
        BookingDbContext dbContext,
        IAvailabilityApi availabilityApi,
        IEventOutbox eventOutbox)
    {
        var clockMock = new Mock<TimeProvider>();
        clockMock.Setup(x => x.GetUtcNow()).Returns(Now);

        var spMock = new Mock<IServiceProvider>();
        spMock.Setup(x => x.GetService(typeof(IBookingRepository))).Returns(repository);
        spMock.Setup(x => x.GetService(typeof(BookingDbContext))).Returns(dbContext);
        spMock.Setup(x => x.GetService(typeof(IAvailabilityApi))).Returns(availabilityApi);
        spMock.Setup(x => x.GetService(typeof(IEventOutbox))).Returns(eventOutbox);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(x => x.ServiceProvider).Returns(spMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(x => x.CreateScope()).Returns(scopeMock.Object);

        return new BookingExpirationService(
            scopeFactoryMock.Object,
            clockMock.Object,
            NullLogger<BookingExpirationService>.Instance);
    }
}
