using CarRental.BuildingBlocks.Results;

namespace CarRental.Modules.Notifications.Errors;

internal static class NotificationsErrors
{
    public static Error InvalidUser => Error.Validation("notification.invalid_user", "User ID is required.");

    public static Error InvalidContent => Error.Validation("notification.invalid_content", "Notification content cannot be empty.");

    public static Error InvalidDedupKey => Error.Validation("notification.invalid_dedup_key", "Deduplication key is required.");

}
