using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Fleet.Contracts;
using CarRental.Modules.Fleet.Contracts.Dtos;
using CarRental.Modules.Handover.Contracts.Enums;
using CarRental.Modules.Handover.Contracts.Events;
using CarRental.Modules.Payments.Contracts.Events;
using CarRental.Modules.Reporting.Data;
using CarRental.Modules.Reporting.Data.Repositories;
using CarRental.Modules.Reporting.Entities;
using CarRental.Modules.Reporting.EventHandlers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CarRental.Modules.Tests.Reporting;

public class ReportingEventHandlerTests
{
    // 2026-11-15 18:00:00 UTC -> 2026-11-16 01:00:00 UTC+7 (next business day!)
    private static readonly DateTimeOffset LateNightUtc = new(2026, 11, 15, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly ExpectedBucketDate = new(2026, 11, 16);

    [Fact]
    public async Task BookingRequestedConsumer_ShouldUpdateUtcPlus7StatsCreateProjectionAndEnforceIdempotency()
    {
        var harness = CreateHarness();
        var bookingId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        harness.FleetApiMock
            .Setup(x => x.GetVehicleSummaryAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new VehicleSummaryDto(
                vehicleId,
                ownerId,
                "Published",
                Guid.NewGuid(),
                "Mazda",
                Guid.NewGuid(),
                "CX-5",
                "51K-889.99",
                "Auto",
                5,
                null,
                GeoPoint.Create(10.7769, 106.7009).Value,
                "HCMC")));

        var consumer = new BookingRequestedConsumer();
        var message = new BookingRequested(
            eventId,
            LateNightUtc,
            bookingId,
            Guid.NewGuid(),
            ownerId,
            vehicleId,
            DateTimeRange.Create(LateNightUtc.AddDays(1), LateNightUtc.AddDays(3)).Value,
            new Money(3_400_000L, "VND"),
            new Money(1_000_000L, "VND"));

        await consumer.Handle(
            message,
            harness.RepositoryMock.Object,
            harness.DbContextMock.Object,
            harness.FleetApiMock.Object,
            harness.ClockMock.Object,
            CancellationToken.None);

        harness.Stats.Should().ContainKey(ExpectedBucketDate);
        harness.Stats[ExpectedBucketDate].TotalBookings.Should().Be(1);
        harness.Projections.Should().ContainKey(bookingId);

        var projection = harness.Projections[bookingId];
        projection.OwnerId.Should().Be(ownerId);
        projection.VehicleId.Should().Be(vehicleId);
        projection.VehiclePlate.Should().Be("51K-889.99");
        projection.VehicleModel.Should().Be("Mazda CX-5");
        projection.RentAmount.Should().Be(3_400_000m);
        projection.PaidAmount.Should().Be(0m);
        projection.Status.Should().Be(BookingProjectionStatus.Pending);

        harness.ProcessedEvents.Should().ContainKey(eventId);
        harness.DbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Replay same event -> idempotent no-op
        await consumer.Handle(
            message,
            harness.RepositoryMock.Object,
            harness.DbContextMock.Object,
            harness.FleetApiMock.Object,
            harness.ClockMock.Object,
            CancellationToken.None);

        harness.Stats[ExpectedBucketDate].TotalBookings.Should().Be(1);
        harness.DbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BookingRequestedConsumer_WhenFleetApiFails_ShouldFallbackToEmptyVehicleMetadata()
    {
        var harness = CreateHarness();
        var bookingId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        harness.FleetApiMock
            .Setup(x => x.GetVehicleSummaryAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<VehicleSummaryDto>(Error.NotFound("fleet.not_found", "Missing")));

        var consumer = new BookingRequestedConsumer();
        var message = new BookingRequested(
            Guid.NewGuid(),
            LateNightUtc,
            bookingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            vehicleId,
            DateTimeRange.Create(LateNightUtc.AddDays(1), LateNightUtc.AddDays(2)).Value,
            new Money(1_200_000L, "VND"),
            new Money(500_000L, "VND"));

        await consumer.Handle(
            message,
            harness.RepositoryMock.Object,
            harness.DbContextMock.Object,
            harness.FleetApiMock.Object,
            harness.ClockMock.Object,
            CancellationToken.None);

        harness.Projections[bookingId].VehiclePlate.Should().BeEmpty();
        harness.Projections[bookingId].VehicleModel.Should().BeEmpty();
    }

    [Fact]
    public async Task PaymentSucceededConsumer_ShouldAddRevenueUpdateProjectionAndEnforceIdempotency()
    {
        var harness = CreateHarness();
        var bookingId = Guid.NewGuid();
        var projection = BookingReportProjection.Create(
            bookingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "51A-12345",
            "Toyota Camry",
            LateNightUtc.AddDays(1),
            LateNightUtc.AddDays(3),
            2_500_000m,
            LateNightUtc);
        harness.Projections[bookingId] = projection;

        var consumer = new PaymentSucceededConsumer();
        var eventId = Guid.NewGuid();
        var message = new PaymentSucceeded(
            eventId,
            LateNightUtc,
            bookingId,
            Guid.NewGuid(),
            new Money(2_500_000L, "VND"));

        await consumer.Handle(
            message,
            harness.RepositoryMock.Object,
            harness.DbContextMock.Object,
            harness.ClockMock.Object,
            CancellationToken.None);

        harness.Stats[ExpectedBucketDate].TotalRevenue.Should().Be(2_500_000m);
        projection.PaidAmount.Should().Be(2_500_000m);
        projection.PaidAt.Should().Be(LateNightUtc);
        projection.Status.Should().Be(BookingProjectionStatus.Paid);
        harness.DbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Replay -> idempotent
        await consumer.Handle(
            message,
            harness.RepositoryMock.Object,
            harness.DbContextMock.Object,
            harness.ClockMock.Object,
            CancellationToken.None);

        harness.Stats[ExpectedBucketDate].TotalRevenue.Should().Be(2_500_000m);
        projection.PaidAmount.Should().Be(2_500_000m);
        harness.DbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PaymentRefundedConsumer_ShouldDeductRevenueAndPaidAmountAndEnforceIdempotency()
    {
        var harness = CreateHarness();
        var bookingId = Guid.NewGuid();
        var stats = DailyBookingStats.Create(ExpectedBucketDate);
        stats.ApplyPaymentSucceeded(2_500_000m, LateNightUtc);
        harness.Stats[ExpectedBucketDate] = stats;

        var projection = BookingReportProjection.Create(
            bookingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "51A-12345",
            "Toyota Camry",
            LateNightUtc.AddDays(1),
            LateNightUtc.AddDays(3),
            2_500_000m,
            LateNightUtc);
        projection.RecordPayment(2_500_000m, LateNightUtc);
        harness.Projections[bookingId] = projection;

        var consumer = new PaymentRefundedConsumer();
        var eventId = Guid.NewGuid();
        var message = new PaymentRefunded(
            eventId,
            LateNightUtc.AddMinutes(30),
            bookingId,
            Guid.NewGuid(),
            new Money(1_000_000L, "VND"),
            IsFullRefund: false,
            "Partial refund");

        await consumer.Handle(
            message,
            harness.RepositoryMock.Object,
            harness.DbContextMock.Object,
            harness.ClockMock.Object,
            CancellationToken.None);

        harness.Stats[ExpectedBucketDate].TotalRevenue.Should().Be(1_500_000m);
        projection.PaidAmount.Should().Be(1_500_000m);
        harness.DbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Replay -> idempotent
        await consumer.Handle(
            message,
            harness.RepositoryMock.Object,
            harness.DbContextMock.Object,
            harness.ClockMock.Object,
            CancellationToken.None);

        harness.Stats[ExpectedBucketDate].TotalRevenue.Should().Be(1_500_000m);
        projection.PaidAmount.Should().Be(1_500_000m);
        harness.DbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BookingCancelledConsumer_ShouldIncrementCancellationsMarkCancelledAndEnforceIdempotency()
    {
        var harness = CreateHarness();
        var bookingId = Guid.NewGuid();
        var projection = BookingReportProjection.Create(
            bookingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "51A-12345",
            "Toyota Camry",
            LateNightUtc.AddDays(1),
            LateNightUtc.AddDays(3),
            2_500_000m,
            LateNightUtc);
        harness.Projections[bookingId] = projection;

        var consumer = new BookingCancelledConsumer();
        var eventId = Guid.NewGuid();
        var cancelledAt = LateNightUtc.AddHours(1);
        var message = new BookingCancelled(
            eventId,
            cancelledAt,
            bookingId,
            Guid.NewGuid(),
            new Money(0L, "VND"),
            new Money(0L, "VND"),
            "Plans changed");

        await consumer.Handle(
            message,
            harness.RepositoryMock.Object,
            harness.DbContextMock.Object,
            harness.ClockMock.Object,
            CancellationToken.None);

        harness.Stats[ExpectedBucketDate].CancellationCount.Should().Be(1);
        projection.Status.Should().Be(BookingProjectionStatus.Cancelled);
        projection.CancelledAt.Should().Be(cancelledAt);
        harness.DbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Replay -> idempotent
        await consumer.Handle(
            message,
            harness.RepositoryMock.Object,
            harness.DbContextMock.Object,
            harness.ClockMock.Object,
            CancellationToken.None);

        harness.Stats[ExpectedBucketDate].CancellationCount.Should().Be(1);
        harness.DbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportingBookingLifecycleHandlers_ShouldTransitionStatesAndEnforceIdempotency()
    {
        var harness = CreateHarness();
        var handler = new ReportingBookingLifecycleHandlers();

        var booking1 = Guid.NewGuid();
        var proj1 = CreatePendingProjection(booking1);
        harness.Projections[booking1] = proj1;

        var confirmEvent = new BookingConfirmed(
            Guid.NewGuid(), LateNightUtc.AddMinutes(5), booking1,
            new Money(2_000_000L, "VND"), new Money(500_000L, "VND"), LateNightUtc.AddHours(2));
        await handler.Handle(confirmEvent, harness.RepositoryMock.Object, harness.DbContextMock.Object, harness.ClockMock.Object);
        proj1.Status.Should().Be(BookingProjectionStatus.Confirmed);

        var paidEvent = new BookingPaid(
            Guid.NewGuid(), LateNightUtc.AddMinutes(10), booking1,
            Guid.NewGuid(), proj1.OwnerId, proj1.VehicleId,
            DateTimeRange.Create(proj1.StartTime, proj1.EndTime).Value);
        await handler.Handle(paidEvent, harness.RepositoryMock.Object, harness.DbContextMock.Object, harness.ClockMock.Object);
        proj1.Status.Should().Be(BookingProjectionStatus.Paid);
        proj1.PaidAt.Should().Be(paidEvent.OccurredAt);

        var pickupEvent = new HandoverConfirmed(
            Guid.NewGuid(), LateNightUtc.AddDays(1), booking1,
            nameof(HandoverType.Pickup), 10_000, 80, LateNightUtc.AddDays(1));
        await handler.Handle(pickupEvent, harness.RepositoryMock.Object, harness.DbContextMock.Object, harness.ClockMock.Object);
        proj1.Status.Should().Be(BookingProjectionStatus.InProgress);

        // Return handover does not change InProgress status directly (waits for BookingCompleted)
        var returnEvent = new HandoverConfirmed(
            Guid.NewGuid(), LateNightUtc.AddDays(3), booking1,
            nameof(HandoverType.Return), 10_350, 75, LateNightUtc.AddDays(3), 350);
        await handler.Handle(returnEvent, harness.RepositoryMock.Object, harness.DbContextMock.Object, harness.ClockMock.Object);
        proj1.Status.Should().Be(BookingProjectionStatus.InProgress);

        var completedEvent = new BookingCompleted(
            Guid.NewGuid(), LateNightUtc.AddDays(3).AddMinutes(5), booking1,
            proj1.VehicleId, Guid.NewGuid(), proj1.OwnerId);
        await handler.Handle(completedEvent, harness.RepositoryMock.Object, harness.DbContextMock.Object, harness.ClockMock.Object);
        proj1.Status.Should().Be(BookingProjectionStatus.Completed);
        proj1.CompletedAt.Should().Be(completedEvent.OccurredAt);

        // Rejected & Expired on separate bookings
        var booking2 = Guid.NewGuid();
        var proj2 = CreatePendingProjection(booking2);
        harness.Projections[booking2] = proj2;

        var rejectedEvent = new BookingRejected(Guid.NewGuid(), LateNightUtc.AddMinutes(15), booking2, proj2.OwnerId, "Busy");
        await handler.Handle(rejectedEvent, harness.RepositoryMock.Object, harness.DbContextMock.Object, harness.ClockMock.Object);
        proj2.Status.Should().Be(BookingProjectionStatus.Rejected);

        var booking3 = Guid.NewGuid();
        var proj3 = CreatePendingProjection(booking3);
        harness.Projections[booking3] = proj3;

        var expiredEvent = new BookingExpired(Guid.NewGuid(), LateNightUtc.AddMinutes(30), booking3, "Timeout");
        await handler.Handle(expiredEvent, harness.RepositoryMock.Object, harness.DbContextMock.Object, harness.ClockMock.Object);
        proj3.Status.Should().Be(BookingProjectionStatus.Expired);

        // Replay completed event -> SaveChangesAsync call count does not increase
        var saveCountBeforeReplay = harness.SaveCalls;
        await handler.Handle(completedEvent, harness.RepositoryMock.Object, harness.DbContextMock.Object, harness.ClockMock.Object);
        harness.SaveCalls.Should().Be(saveCountBeforeReplay);
    }

    private static BookingReportProjection CreatePendingProjection(Guid bookingId) =>
        BookingReportProjection.Create(
            bookingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "51F-111.11",
            "Honda Civic",
            LateNightUtc.AddDays(1),
            LateNightUtc.AddDays(3),
            2_000_000m,
            LateNightUtc);

    private sealed class TestHarness
    {
        public Dictionary<Guid, ProcessedEvent> ProcessedEvents { get; } = [];
        public Dictionary<DateOnly, DailyBookingStats> Stats { get; } = [];
        public Dictionary<Guid, BookingReportProjection> Projections { get; } = [];
        public Mock<IReportingRepository> RepositoryMock { get; } = new();
        public Mock<ReportingDbContext> DbContextMock { get; } =
            new(new DbContextOptionsBuilder<ReportingDbContext>().Options);
        public Mock<IFleetApi> FleetApiMock { get; } = new();
        public Mock<TimeProvider> ClockMock { get; } = new();
        public int SaveCalls { get; set; }
    }

    private static TestHarness CreateHarness()
    {
        var h = new TestHarness();
        h.ClockMock.Setup(x => x.GetUtcNow()).Returns(LateNightUtc);
        h.DbContextMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => h.SaveCalls++)
            .ReturnsAsync(1);

        h.RepositoryMock
            .Setup(x => x.HasEventBeenProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => h.ProcessedEvents.ContainsKey(id));
        h.RepositoryMock
            .Setup(x => x.MarkEventProcessed(It.IsAny<ProcessedEvent>()))
            .Callback((ProcessedEvent pe) => h.ProcessedEvents[pe.Id] = pe);
        h.RepositoryMock
            .Setup(x => x.GetDailyStatsAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateOnly d, CancellationToken _) => h.Stats.GetValueOrDefault(d));
        h.RepositoryMock
            .Setup(x => x.AddDailyStats(It.IsAny<DailyBookingStats>()))
            .Callback((DailyBookingStats s) => h.Stats[s.Id] = s);
        h.RepositoryMock
            .Setup(x => x.GetBookingProjectionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => h.Projections.GetValueOrDefault(id));
        h.RepositoryMock
            .Setup(x => x.AddBookingProjection(It.IsAny<BookingReportProjection>()))
            .Callback((BookingReportProjection p) => h.Projections[p.Id] = p);

        return h;
    }
}
