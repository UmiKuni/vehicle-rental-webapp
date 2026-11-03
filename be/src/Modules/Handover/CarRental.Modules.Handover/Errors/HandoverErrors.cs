using CarRental.BuildingBlocks.Results;

namespace CarRental.Modules.Handover.Errors;

internal static class HandoverErrors
{
    public static Error InvalidContext => Error.Validation("handover.invalid_context", "Handover context is required.");

    public static Error InactiveContext => Error.Validation("handover.inactive_context", "Booking is not active for handover.");

    public static Error Unauthorized5CEF8A => Error.Unauthorized("handover.unauthorized", "Only the customer or owner can initiate handover.");

    public static Error ImmutableB8F729 => Error.Validation("handover.immutable", "Photos can only be added to draft handovers.");

    public static Error InvalidPhoto => Error.Validation("handover.invalid_photo", "Photo storage key cannot be empty.");

    public static Error MissingHash => Error.Validation("handover.missing_hash", "Tamper-verification file hash is mandatory.");

    public static Error ImmutableA1098D => Error.Validation("handover.immutable", "Vehicle condition can only be updated for draft handovers.");

    public static Error InvalidOdometer => Error.Validation("handover.invalid_odometer", "Odometer reading cannot be negative.");

    public static Error InvalidFuel => Error.Validation("handover.invalid_fuel", "Fuel level must be between 0 and 100 percent.");

    public static Error AlreadyConfirmed => Error.Conflict("handover.already_confirmed", "Handover has already been confirmed and is immutable.");

    public static Error UnauthorizedE2911A => Error.Unauthorized("handover.unauthorized", "Only the customer or owner can confirm this handover.");

    public static Error MissingCondition => Error.Validation("handover.missing_condition", "Vehicle condition must be recorded before confirmation.");

}
