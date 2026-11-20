using CarRental.BuildingBlocks.Results;

namespace CarRental.Modules.Audit.Errors;

internal static class AuditErrors
{
    public static Error EmptyEventId => Error.Validation(
        "audit.empty_event_id",
        "Audit event ID must not be empty.");

    public static Error EmptyTimestamp => Error.Validation(
        "audit.empty_timestamp",
        "Audit timestamp must be provided explicitly.");

    public static Error EmptyActorId => Error.Validation(
        "audit.empty_actor_id",
        "Actor ID must not be empty.");

    public static Error EmptyAction => Error.Validation(
        "audit.empty_action",
        "Action must not be empty.");

    public static Error EmptyEntityName => Error.Validation(
        "audit.empty_entity_name",
        "Entity name must not be empty.");

    public static Error EmptyEntityId => Error.Validation(
        "audit.empty_entity_id",
        "Entity ID must not be empty.");
}
