using CarRental.Modules.Notifications.Contracts.Enums;
using CarRental.Modules.Notifications.Entities;
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
}
