using CarRental.BuildingBlocks.Results;

namespace CarRental.Modules.Support.Errors;

internal static class SupportErrors
{
    public static Error InvalidBooking => Error.Validation("support.invalid_booking", "Booking ID is required.");

    public static Error InvalidReporter => Error.Validation("support.invalid_reporter", "Reporter ID is required.");

    public static Error EmptyMessage98E994 => Error.Validation("support.empty_message", "An initial message describing the issue is required.");

    public static Error TicketResolved => Error.Conflict("support.ticket_resolved", "Cannot add messages to a resolved ticket.");

    public static Error InvalidSender => Error.Validation("support.invalid_sender", "Sender ID is required.");

    public static Error EmptyMessageE27D76 => Error.Validation("support.empty_message", "Message content cannot be empty.");

    public static Error InvalidAdmin => Error.Validation("support.invalid_admin", "Admin ID is required.");

    public static Error AlreadyResolved => Error.Conflict("support.already_resolved", "Ticket has already been resolved.");

    public static Error InvalidPercent => Error.Validation("support.invalid_percent", "Customer deposit refund percentage must be between 0 and 100.");

    public static Error MissingResolutionNote => Error.Validation("support.missing_resolution_note", "Resolution ruling note is required.");

}
