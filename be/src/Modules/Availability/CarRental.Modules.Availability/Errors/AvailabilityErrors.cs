using CarRental.BuildingBlocks.Results;

namespace CarRental.Modules.Availability.Errors;

internal static class AvailabilityErrors
{
    public static Error InvalidBooking => Error.Validation("availability.invalid_booking", "Booking ID is required.");

    public static Error InvalidRange => Error.Validation("availability.invalid_range", "Time range is required.");

    public static Error PastDate => Error.Validation("availability.past_date", "Time range must start in the future.");

    public static Error SlotUnavailable3493D3 => Error.Validation("availability.slot_unavailable", "The requested time slot is not available.");

    public static Error HoldNotFoundOrExpired => Error.NotFound("availability.hold_not_found_or_expired", "Active hold for booking was not found or has expired.");

    public static Error InvalidBlockType => Error.Validation("availability.invalid_block_type", "Only OwnerBlocked or Maintenance blocks are allowed.");

    public static Error SlotUnavailableEA8C70 => Error.Validation("availability.slot_unavailable", "Cannot block over existing active bookings or holds.");

    public static Error EntryNotFound => Error.NotFound("availability.entry_not_found", "Calendar entry not found.");

    public static Error InvalidEntryType => Error.Validation("availability.invalid_entry_type", "Only owner or maintenance blocks can be unblocked directly.");

}
