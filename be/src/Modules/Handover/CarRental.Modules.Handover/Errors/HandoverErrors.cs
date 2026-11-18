using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Handover.Contracts.Enums;

namespace CarRental.Modules.Handover.Errors;

internal static class HandoverErrors
{
    public static Error InvalidContext =>
        Error.Validation("handover.invalid_context", "Handover context is required.");

    public static Error InactiveContext =>
        Error.Validation("handover.inactive_context", "Booking is not active for handover.");

    public static Error NotParticipant =>
        Error.Forbidden("handover.not_participant", "Only the booking customer or vehicle owner can access this handover.");

    public static Error Immutable =>
        Error.Conflict("handover.immutable", "Confirmed handovers are immutable.");

    public static Error InvalidPhoto =>
        Error.Validation("handover.invalid_photo", "Photo storage key cannot be empty.");

    public static Error MissingHash =>
        Error.Validation("handover.missing_hash", "Tamper-verification file hash is mandatory.");

    public static Error InvalidOdometer =>
        Error.Validation("handover.invalid_odometer", "Odometer reading cannot be negative.");

    public static Error InvalidFuel =>
        Error.Validation("handover.invalid_fuel", "Fuel level must be between 0 and 100 percent.");

    public static Error AlreadyConfirmed =>
        Error.Conflict("handover.already_confirmed", "Handover has already been confirmed and is immutable.");

    public static Error MissingCondition =>
        Error.Validation("handover.missing_condition", "Vehicle condition must be recorded before confirmation.");

    public static Error OdometerDecreased(int returnOdo, int pickupOdo) =>
        Error.Validation(
            "handover.odometer_decreased",
            $"Return odometer ({returnOdo}) cannot be less than pickup odometer ({pickupOdo}).");

    public static Error MissingMandatoryPhoto(PhotoCorner corner) =>
        Error.Validation(
            "handover.missing_mandatory_photo",
            $"Missing mandatory photo for corner: {corner}.");

    public static Error NotFound =>
        Error.NotFound("handover.not_found", "Handover record was not found.");

    public static Error InvalidType =>
        Error.Validation("handover.invalid_type", "Type must be either 'Pickup' or 'Return'.");

    public static Error InvalidCorner =>
        Error.Validation("handover.invalid_corner", "Corner must be one of: FrontLeft, FrontRight, RearLeft, RearRight, Dash.");

    public static Error InvalidRole =>
        Error.Validation("handover.invalid_role", "ConfirmedByRole must be either 'Customer' or 'Owner'.");

    public static Error RoleMismatch =>
        Error.Forbidden("handover.role_mismatch", "Authenticated user does not match the specified confirmation role.");

    public static Error BookingNotReadyForPickup =>
        Error.Conflict("handover.booking_not_ready_for_pickup", "Booking must be in 'Paid' status before creating a pickup handover.");

    public static Error BookingNotInProgress =>
        Error.Conflict("handover.booking_not_in_progress", "Booking must be in 'InProgress' status before creating a return handover.");

    public static Error AlreadyExistsForType =>
        Error.Conflict("handover.already_exists", "A handover of this type already exists for the booking.");
}
