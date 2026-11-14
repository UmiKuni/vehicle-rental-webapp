using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Fleet.Contracts.Events;
using CarRental.Modules.Notifications.Contracts.Enums;
using CarRental.Modules.Notifications.Entities;
using CarRental.Modules.Notifications.EventHandlers;
using CarRental.Modules.Payments.Contracts.Events;
using CarRental.Modules.Support.Contracts.Events;
using FluentAssertions;
using Xunit;

namespace CarRental.Modules.Tests;

public class NotificationsModuleTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        var result = Notification.Create(
            UserId,
            NotificationType.Email,
            "{\"template\":\"BookingConfirmed\",\"bookingId\":\"123\"}",
            "evt_booking_confirmed_123",
            Now);

        result.IsSuccess.Should().BeTrue();
        var notification = result.Value;
        notification.Id.Should().NotBeNull();
        notification.UserId.Should().Be(UserId);
        notification.Type.Should().Be(NotificationType.Email);
        notification.Status.Should().Be(NotificationStatus.Pending);
        notification.Attempts.Should().Be(0);
        notification.DedupKey.Should().Be("evt_booking_confirmed_123");
        notification.CreatedAt.Should().Be(Now);
        notification.SentAt.Should().BeNull();
        notification.LastError.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimContentAndDedupKey()
    {
        var result = Notification.Create(
            UserId,
            NotificationType.SMS,
            "   Your OTP is 123456   ",
            "   dedup_key_trim   ",
            Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("Your OTP is 123456");
        result.Value.DedupKey.Should().Be("dedup_key_trim");
    }

    [Theory]
    [InlineData(NotificationType.Email)]
    [InlineData(NotificationType.SMS)]
    [InlineData(NotificationType.Push)]
    public void Create_AllNotificationTypes_ShouldSucceed(NotificationType type)
    {
        var result = Notification.Create(UserId, type, "Alert message", $"key_{type}", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(type);
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldFail()
    {
        var result = Notification.Create(
            Guid.Empty,
            NotificationType.SMS,
            "{\"code\":\"123456\"}",
            "dedup_key_1",
            Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("notification.invalid_user");
    }

    [Fact]
    public void Create_WithEmptyContent_ShouldFail()
    {
        var result = Notification.Create(
            UserId,
            NotificationType.SMS,
            "   ",
            "dedup_key_2",
            Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("notification.invalid_content");
    }

    [Fact]
    public void Create_WithEmptyDedupKey_ShouldFail()
    {
        var result = Notification.Create(
            UserId,
            NotificationType.Push,
            "{\"title\":\"Vehicle Available\"}",
            "   ",
            Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("notification.invalid_dedup_key");
    }

    [Fact]
    public void MarkSent_ShouldUpdateStatusAndSentAt()
    {
        var notification = Notification.Create(
            UserId,
            NotificationType.Email,
            "{\"test\":true}",
            "dedup_sent_1",
            Now).Value;

        var sentTime = Now.AddSeconds(45);
        notification.MarkSent(sentTime);

        notification.Status.Should().Be(NotificationStatus.Sent);
        notification.SentAt.Should().Be(sentTime);
        notification.LastError.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_FirstTwoAttempts_ShouldIncrementAttemptsAndRemainPending()
    {
        var notification = Notification.Create(
            UserId,
            NotificationType.SMS,
            "{\"otp\":\"987654\"}",
            "dedup_fail_1",
            Now).Value;

        notification.MarkFailed("SMTP timeout");

        notification.Attempts.Should().Be(1);
        notification.Status.Should().Be(NotificationStatus.Pending);
        notification.LastError.Should().Be("SMTP timeout");

        notification.MarkFailed("Network error");

        notification.Attempts.Should().Be(2);
        notification.Status.Should().Be(NotificationStatus.Pending);
        notification.LastError.Should().Be("Network error");
    }

    [Fact]
    public void MarkFailed_WithEmptyError_ShouldFallbackToDefaultErrorMessage()
    {
        var notification = Notification.Create(
            UserId,
            NotificationType.Push,
            "Push payload",
            "dedup_fallback",
            Now).Value;

        notification.MarkFailed("   ");

        notification.LastError.Should().Be("Notification delivery failed");
    }

    [Fact]
    public void MarkFailed_ThreeAttempts_ShouldTransitionToFailed()
    {
        var notification = Notification.Create(
            UserId,
            NotificationType.SMS,
            "{\"otp\":\"987654\"}",
            "dedup_fail_max",
            Now).Value;

        notification.MarkFailed("Attempt 1 failure");
        notification.MarkFailed("Attempt 2 failure");
        notification.MarkFailed("Attempt 3 failure");

        notification.Attempts.Should().Be(3);
        notification.Status.Should().Be(NotificationStatus.Failed);
        notification.LastError.Should().Be("Attempt 3 failure");
    }

    [Fact]
    public void MarkFailed_ThenMarkSent_ShouldClearLastError()
    {
        var notification = Notification.Create(
            UserId,
            NotificationType.Email,
            "Hello",
            "dedup_retry",
            Now).Value;

        notification.MarkFailed("Temporary glitch");
        notification.Attempts.Should().Be(1);
        notification.LastError.Should().Be("Temporary glitch");

        var sentTime = Now.AddMinutes(1);
        notification.MarkSent(sentTime);

        notification.Status.Should().Be(NotificationStatus.Sent);
        notification.SentAt.Should().Be(sentTime);
        notification.LastError.Should().BeNull();
    }

    // =========================================================================
    // Wolverine Event Consumer Invocations (10 Consumers)
    // =========================================================================

    [Fact]
    public async Task BookingConsumers_ShouldExecuteSuccessfully()
    {
        var eventId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var money = new Money(500_000, "VND");
        var period = DateTimeRange.Create(Now.AddDays(1), Now.AddDays(3)).Value;

        // 1. BookingRequestedConsumer
        var reqConsumer = new BookingRequestedConsumer();
        await reqConsumer.Consume(new BookingRequested(
            eventId, Now, bookingId, customerId, ownerId, vehicleId, period, money, money));

        // 2. BookingConfirmedConsumer
        var confConsumer = new BookingConfirmedConsumer();
        await confConsumer.Consume(new BookingConfirmed(
            eventId, Now, bookingId, money, money, Now.AddHours(2)));

        // 3. BookingRejectedConsumer
        var rejConsumer = new BookingRejectedConsumer();
        await rejConsumer.Consume(new BookingRejected(
            eventId, Now, bookingId, ownerId, "Vehicle unavailable"));

        // 4. BookingCancelledConsumer
        var cancelConsumer = new BookingCancelledConsumer();
        await cancelConsumer.Consume(new BookingCancelled(
            eventId, Now, bookingId, customerId, Money.Zero("VND"), money, "Customer changed plans"));

        // 5. BookingExpiredConsumer
        var expConsumer = new BookingExpiredConsumer();
        await expConsumer.Consume(new BookingExpired(
            eventId, Now, bookingId, "Payment hold timed out"));

        Assert.True(true);
    }

    [Fact]
    public async Task PaymentConsumers_ShouldExecuteSuccessfully()
    {
        var eventId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var amount = new Money(1_200_000, "VND");

        // 6. PaymentSucceededConsumer
        var successConsumer = new PaymentSucceededConsumer();
        await successConsumer.Consume(new PaymentSucceeded(eventId, Now, bookingId, paymentId, amount));

        // 7. PaymentFailedConsumer
        var failConsumer = new PaymentFailedConsumer();
        await failConsumer.Consume(new PaymentFailed(eventId, Now, bookingId, paymentId, "Card declined"));

        Assert.True(true);
    }

    [Fact]
    public async Task SupportAndFleetConsumers_ShouldExecuteSuccessfully()
    {
        var eventId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var location = GeoPoint.Create(10.7769, 106.7009).Value;

        // 8. TicketResolvedConsumer
        var ticketConsumer = new TicketResolvedConsumer();
        await ticketConsumer.Consume(new TicketResolved(
            eventId, Now, ticketId, bookingId, 100, "Full refund issued", adminId));

        // 9. VehiclePublishedConsumer
        var pubConsumer = new VehiclePublishedConsumer();
        await pubConsumer.Consume(new VehiclePublished(
            eventId, Now, vehicleId, ownerId, "Toyota", "Corolla", 5, "Auto", location, "District 1, HCMC"));

        // 10. VehicleUnpublishedConsumer
        var unpubConsumer = new VehicleUnpublishedConsumer();
        await unpubConsumer.Consume(new VehicleUnpublished(
            eventId, Now, vehicleId, ownerId, "Admin unpublish"));

        Assert.True(true);
    }
}
