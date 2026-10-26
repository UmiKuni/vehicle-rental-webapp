namespace CarRental.Modules.Audit.Contracts.Commands;

/// <summary>
/// Command contract dispatched asynchronously by application pipeline behaviors
/// to record an immutable audit trail entry for audited actions.
/// </summary>
public sealed record RecordAuditEntry(
    Guid EventId,
    DateTimeOffset Timestamp,
    Guid ActorId,
    string IpAddress,
    string Action,
    string EntityName,
    string EntityId,
    string? BeforeStateJson,
    string? AfterStateJson);
