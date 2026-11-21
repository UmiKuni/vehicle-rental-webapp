using CarRental.BuildingBlocks.Results;

namespace CarRental.Modules.Reporting.Errors;

internal static class ReportingErrors
{
    public static Error InvalidDateRange =>
        Error.Validation("reporting.invalid_date_range", "End date must be on or after start date.");

    public static Error RangeTooLarge =>
        Error.Validation("reporting.range_too_large", "Date range cannot exceed 366 days.");

    public static Error InvalidOwner =>
        Error.Validation("reporting.invalid_owner", "Owner ID is required.");

    public static Error InvalidPeriod =>
        Error.Validation("reporting.invalid_period", "Month must be 1-12 and year must be valid.");

    public static Error ForbiddenOwnerAccess =>
        Error.Forbidden("reporting.forbidden_owner_access", "Owners can only view their own revenue report.");
}
