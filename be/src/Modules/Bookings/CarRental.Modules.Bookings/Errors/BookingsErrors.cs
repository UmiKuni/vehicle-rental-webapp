using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Bookings.Contracts.Enums;

namespace CarRental.Modules.Bookings.Errors;

internal static class BookingsErrors
{
    public static Error InvalidCustomer => Error.Validation("booking.invalid_customer", "Customer ID is required.");

    public static Error InvalidOwner => Error.Validation("booking.invalid_owner", "Owner ID is required.");

    public static Error InvalidVehicle => Error.Validation("booking.invalid_vehicle", "Vehicle ID is required.");

    public static Error CustomerIsOwner => Error.Validation("booking.customer_is_owner", "Customer cannot rent their own vehicle.");

    public static Error InvalidPeriod => Error.Validation("booking.invalid_period", "Rental period is required.");

    public static Error StartInPast => Error.Validation("booking.start_in_past", "Rental start time must be in the future.");

    public static Error InvalidDuration => Error.Validation("booking.invalid_duration", "End time must be after start time.");

    public static Error InvalidLocation => Error.Validation("booking.invalid_location", "Pickup location is required.");

    public static Error MissingLineItems => Error.Validation("booking.missing_line_items", "Line items are required.");

    public static Error MissingRentItem => Error.Validation("booking.missing_rent_item", "At least one Rent line item is required.");

    public static Error MissingDepositItem => Error.Validation("booking.missing_deposit_item", "At least one Deposit line item is required.");

    public static Error MissingPolicy => Error.Validation("booking.missing_policy", "Cancellation policy snapshot is required.");

    public static Error NotOwner => Error.Forbidden("booking.not_owner", "Only the vehicle owner can perform this action on the booking.");

    public static Error NotCustomer => Error.Forbidden("booking.not_customer", "Only the booking customer can cancel this booking.");

    public static Error NotParticipant => Error.Forbidden("booking.not_participant", "Only the customer, vehicle owner, or an administrator can access this booking.");

    public static Error TripInProgress => Error.Conflict("booking.trip_in_progress", "Cannot cancel a trip that is currently in progress.");

    public static Error InvalidAdmin => Error.Validation("booking.invalid_admin", "Admin ID is required.");

    public static Error OverKmFeeRequiresCompleted => Error.Conflict("booking.over_km_requires_completed", "Over-km fee can only be added to completed bookings.");

    public static Error InvalidFee => Error.Validation("booking.invalid_fee", "Over-km fee amount must be greater than zero.");

    public static Error NotFound => Error.NotFound("booking.not_found", "Booking was not found.");

    public static Error VehicleNotPublished => Error.Conflict("booking.vehicle_not_published", "Vehicle must be published before it can be booked.");

    public static Error ReasonRequired => Error.Validation("booking.reason_required", "A reason is required for this operation.");

    public static Error InvalidStatusFilter => Error.Validation("booking.invalid_status_filter", "The specified booking status filter is invalid.");

    public static Error InvalidPenaltyParty => Error.Validation("booking.invalid_penalty_party", "Penalty party must be 'Customer', 'Owner', or 'None'.");

    public static Error CustomerNotEligible(string reason) =>
        Error.Validation(
            "booking.customer_not_eligible",
            string.IsNullOrWhiteSpace(reason) ? "Customer is not eligible to create a booking." : reason);

    public static Error InvalidState(string expected, BookingStatus current) =>
        Error.Conflict(
            "booking.invalid_state",
            $"Expected booking status {expected}, but current status is {current}.");

    public static Error TerminalState(BookingStatus current) =>
        Error.Conflict(
            "booking.terminal_state",
            $"Cannot modify a booking in terminal state {current}.");

    public static Error ConcurrentModification =>
        Error.Conflict(
            "booking.concurrent_modification",
            "The booking was modified concurrently. Please refresh and try again.");
}
