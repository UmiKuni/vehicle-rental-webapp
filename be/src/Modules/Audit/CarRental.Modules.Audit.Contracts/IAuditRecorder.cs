namespace CarRental.Modules.Audit.Contracts;

/// <summary>
/// Service contract to record audit entries across application endpoints and services.
/// Captures actor ID, IP address, timestamp, and dispatches an immutable audit entry
/// via the messaging outbox only for successful operations.
/// </summary>
public interface IAuditRecorder
{
    Task RecordAsync(
        string action,
        string entityName,
        string entityId,
        object? beforeState = null,
        object? afterState = null,
        Guid? actorId = null,
        string? ipAddress = null,
        Guid? eventId = null,
        CancellationToken ct = default);

    Task RecordAsync(
        IAuditedAction action,
        object? beforeState = null,
        object? afterState = null,
        Guid? actorId = null,
        string? ipAddress = null,
        CancellationToken ct = default);
}
