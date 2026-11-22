namespace CarRental.Modules.Audit.Contracts;

/// <summary>
/// Immutable record implementation of <see cref="IAuditedAction"/>.
/// </summary>
public sealed record AuditedAction(
    string Action,
    string EntityName,
    string EntityId,
    bool IncludeBeforeState = false,
    bool IncludeAfterState = false,
    Guid? EventId = null) : IAuditedAction;
