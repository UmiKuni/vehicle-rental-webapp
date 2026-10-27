using CarRental.BuildingBlocks.Results;

namespace CarRental.Modules.Pricing.Errors;

internal static class PricingErrors
{
    public static Error InvalidVehicle => Error.Validation("pricing.invalid_vehicle", "Vehicle ID is required.");

    public static Error InvalidWeekdayRate => Error.Validation("pricing.invalid_weekday_rate", "Weekday rate must be greater than zero.");

    public static Error InvalidWeekendRate => Error.Validation("pricing.invalid_weekend_rate", "Weekend rate must be greater than zero.");

    public static Error InvalidHourlyRate => Error.Validation("pricing.invalid_hourly_rate", "Hourly rate must be greater than zero.");

    public static Error InvalidDeposit => Error.Validation("pricing.invalid_deposit", "Deposit amount cannot be negative.");

    public static Error InvalidKmLimit => Error.Validation("pricing.invalid_km_limit", "Daily KM limit must be greater than zero.");

    public static Error InvalidFeeAmount => Error.Validation("pricing.invalid_fee_amount", "Fee amount cannot be negative.");

    public static Error InvalidPeriod => Error.Validation("pricing.invalid_period", "Rental period is required.");

    public static Error OwnerRequired => Error.Validation("pricing.owner_required", "Only the vehicle owner can configure pricing.");

}
