using CarRental.BuildingBlocks.Results;

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

    public static Error Unauthorized5E2B0A => Error.Unauthorized("booking.unauthorized", "Only the vehicle owner can approve this booking.");

    public static Error UnauthorizedBDDFB3 => Error.Unauthorized("booking.unauthorized", "Only the vehicle owner can reject this booking.");

    public static Error Unauthorized6779FC => Error.Unauthorized("booking.unauthorized", "Only the booking customer can cancel this booking.");

    public static Error TripInProgress => Error.Validation("booking.trip_in_progress", "Cannot cancel a trip that is currently in progress.");

    public static Error InvalidAdmin => Error.Validation("booking.invalid_admin", "Admin ID is required.");

    public static Error InvalidState => Error.Validation("booking.invalid_state", "Over-km fee can only be added to completed bookings.");

    public static Error InvalidFee => Error.Validation("booking.invalid_fee", "Over-km fee amount must be greater than zero.");

}
