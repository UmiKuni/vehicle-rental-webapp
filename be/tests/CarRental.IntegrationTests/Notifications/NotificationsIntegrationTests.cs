using CarRental.IntegrationTests.Fixtures;
using CarRental.Modules.Notifications.Contracts.Enums;
using CarRental.Modules.Notifications.Data;
using CarRental.Modules.Notifications.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarRental.IntegrationTests.Notifications;

public class NotificationsIntegrationTests : IntegrationTestBase
{
    public NotificationsIntegrationTests(CarRentalApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Notification_SaveAndQuery_ShouldPersistInPostgreSQL()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var dedupKey = $"booking_confirmed_{Guid.NewGuid():N}";
        var content = "{\"bookingId\":\"12345\",\"status\":\"Confirmed\"}";

        var notification = Notification.Create(
            userId,
            NotificationType.Email,
            content,
            dedupKey,
            now).Value;

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();
        }

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            var persisted = await db.Notifications.FirstOrDefaultAsync(n => n.DedupKey == dedupKey);

            persisted.Should().NotBeNull();
            persisted!.UserId.Should().Be(userId);
            persisted.Type.Should().Be(NotificationType.Email);
            persisted.Status.Should().Be(NotificationStatus.Pending);
            persisted.Attempts.Should().Be(0);
            persisted.Content.Should().Be(content);
            persisted.CreatedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
            persisted.SentAt.Should().BeNull();
        }
    }

    [Fact]
    public async Task Notification_LifecycleTransitions_ShouldUpdateInDatabase()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var dedupKey = $"retry_test_{Guid.NewGuid():N}";

        var notification = Notification.Create(
            userId,
            NotificationType.SMS,
            "OTP: 654321",
            dedupKey,
            now).Value;

        // 1. Initial insert
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();
        }

        // 2. Mark failure
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            var tracked = await db.Notifications.FirstAsync(n => n.DedupKey == dedupKey);
            tracked.MarkFailed("Carrier timeout");
            await db.SaveChangesAsync();
        }

        // 3. Verify failure state in DB
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            var failed = await db.Notifications.FirstAsync(n => n.DedupKey == dedupKey);
            failed.Attempts.Should().Be(1);
            failed.Status.Should().Be(NotificationStatus.Pending);
            failed.LastError.Should().Be("Carrier timeout");

            // 4. Mark success on retry
            var sentTime = DateTimeOffset.UtcNow;
            failed.MarkSent(sentTime);
            await db.SaveChangesAsync();
        }

        // 5. Verify final sent state
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            var sent = await db.Notifications.FirstAsync(n => n.DedupKey == dedupKey);
            sent.Status.Should().Be(NotificationStatus.Sent);
            sent.SentAt.Should().NotBeNull();
            sent.LastError.Should().BeNull();
        }
    }
}
