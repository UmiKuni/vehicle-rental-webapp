using CarRental.BuildingBlocks.Results;

namespace CarRental.Modules.Search.Errors;

internal static class SearchErrors
{
    public static Error InvalidPaging => Error.Validation("search.invalid_paging", "Page must be at least 1 and page size must be between 1 and 100.");

    public static Error InvalidPriceRange => Error.Validation("search.invalid_price_range", "Price range is invalid.");

    public static Error InvalidLocation => Error.Validation("search.invalid_location", "Latitude, longitude, and a positive radius are required together.");

}
