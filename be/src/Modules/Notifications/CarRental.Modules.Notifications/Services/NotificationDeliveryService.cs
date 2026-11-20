using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Notifications.Contracts.Enums;
using CarRental.Modules.Notifications.Data;
using CarRental.Modules.Notifications.Entities;
using CarRental.Modules.Users.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Notifications.Services;

public sealed class NotificationDeliveryService(
    NotificationsDbContext dbContext,
    IUsersApi usersApi,
    INotificationDeliveryProvider provider,
    TimeProvider clock)
{
    public async Task<Result<Notification>> CreateAndDeliverAsync(
        Guid userId,
        NotificationType type,
        string content,
        string dedupKey,
        DateTimeOffset occurredAt,
        CancellationToken ct = default)
    {
        var existing = await dbContext.Notifications
            .FirstOrDefaultAsync(item => item.DedupKey == dedupKey, ct);

        if (existing is not null)
        {
            if (existing.Status == NotificationStatus.Sent ||
                existing.Status == NotificationStatus.Failed)
                return Result.Success(existing);
        }
        else
        {
            var result = Notification.Create(userId, type, content, dedupKey, occurredAt);
            if (result.IsFailure)
                return Result.Failure<Notification>(result.Error);

            existing = result.Value;
            dbContext.Notifications.Add(existing);
            await dbContext.SaveChangesAsync(ct);
        }

        try
        {
            var contactResult = await usersApi.GetContactAsync(existing.UserId, ct);
            if (contactResult.IsFailure)
                throw new NotificationDeliveryException(contactResult.Error.Message);

            await provider.DeliverAsync(existing, contactResult.Value, ct);
            existing.MarkSent(clock.GetUtcNow());
            await dbContext.SaveChangesAsync(ct);
            return Result.Success(existing);
        }
        catch (NotificationDeliveryException)
        {
            existing.MarkFailed("Notification delivery failed.");
            await dbContext.SaveChangesAsync(ct);
            throw;
        }
    }
}
