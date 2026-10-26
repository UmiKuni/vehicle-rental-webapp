namespace CarRental.Modules.Notifications.Entities;

public readonly record struct NotificationId(Guid Value)
{
    public static NotificationId New() => new(Guid.CreateVersion7());
    public static NotificationId From(Guid value) => new(value);
    public static implicit operator Guid(NotificationId id) => id.Value;
    public static implicit operator NotificationId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
