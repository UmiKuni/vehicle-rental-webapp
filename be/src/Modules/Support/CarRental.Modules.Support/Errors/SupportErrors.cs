using CarRental.BuildingBlocks.Results;

namespace CarRental.Modules.Support.Errors;

internal static class SupportErrors
{
    public static Error InvalidBooking => Error.Validation("support.invalid_booking", "Booking ID is required.");
    public static Error NotFound => Error.NotFound("support.not_found", "Support ticket was not found.");
    public static Error Forbidden => Error.Forbidden("support.forbidden", "You are not allowed to access this support ticket.");
    public static Error NotBookingParticipant => Error.Forbidden("support.not_booking_participant", "Only booking participants can open a support ticket.");
    public static Error InvalidPaging => Error.Validation("support.invalid_paging", "Page must be at least 1 and page size must be between 1 and 100.");

    public static Error InvalidReporter => Error.Validation("support.invalid_reporter", "Reporter ID is required.");

    public static Error EmptyMessage => Error.Validation("support.empty_message", "Message content cannot be empty.");

    public static Error TicketResolved => Error.Conflict("support.ticket_resolved", "Cannot add messages to a resolved ticket.");

    public static Error InvalidSender => Error.Validation("support.invalid_sender", "Sender ID is required.");

    public static Error InvalidAdmin => Error.Validation("support.invalid_admin", "Admin ID is required.");

    public static Error AlreadyResolved => Error.Conflict("support.already_resolved", "Ticket has already been resolved.");

    public static Error InvalidPercent => Error.Validation("support.invalid_percent", "Customer deposit refund percentage must be between 0 and 100.");

    public static Error MissingResolutionNote => Error.Validation("support.missing_resolution_note", "Resolution ruling note is required.");

}
