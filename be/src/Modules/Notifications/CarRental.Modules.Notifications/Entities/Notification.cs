using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Notifications.Contracts.Enums;

namespace CarRental.Modules.Notifications.Entities;

public class Notification : AggregateRoot<NotificationId>
{
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public NotificationStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string DedupKey { get; private set; } = string.Empty;

    private Notification() { }

    private Notification(
        NotificationId id,
        Guid userId,
        NotificationType type,
        string content,
        string dedupKey,
        DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        Type = type;
        Content = content;
        DedupKey = dedupKey;
        Status = NotificationStatus.Pending;
        Attempts = 0;
        CreatedAt = now;
    }

    public static Result<Notification> Create(
        Guid userId,
        NotificationType type,
        string content,
        string dedupKey,
        DateTimeOffset now)
    {
        if (userId == Guid.Empty)
            return Result.Failure<Notification>(new Error("notification.invalid_user", "User ID is required."));

        if (string.IsNullOrWhiteSpace(content))
            return Result.Failure<Notification>(new Error("notification.invalid_content", "Notification content cannot be empty."));

        if (string.IsNullOrWhiteSpace(dedupKey))
            return Result.Failure<Notification>(new Error("notification.invalid_dedup_key", "Deduplication key is required."));

        return Result.Success(new Notification(
            NotificationId.New(),
            userId,
            type,
            content.Trim(),
            dedupKey.Trim(),
            now));
    }

    public void MarkSent(DateTimeOffset now)
    {
        Status = NotificationStatus.Sent;
        SentAt = now;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = string.IsNullOrWhiteSpace(error) ? "Notification delivery failed" : error.Trim();

        if (Attempts >= 3)
        {
            Status = NotificationStatus.Failed;
        }
    }
}
