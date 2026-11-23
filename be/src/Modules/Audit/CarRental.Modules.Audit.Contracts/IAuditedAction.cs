namespace CarRental.Modules.Audit.Contracts;

/// <summary>
/// Metadata contract representing an audited action in the system.
/// </summary>
public interface IAuditedAction
{
    string Action { get; }
    string EntityName { get; }
    string EntityId { get; }
    bool IncludeBeforeState { get; }
    bool IncludeAfterState { get; }
    Guid? EventId { get; }
}
