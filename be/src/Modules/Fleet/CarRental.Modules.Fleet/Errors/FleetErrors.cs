using CarRental.BuildingBlocks.Results;

namespace CarRental.Modules.Fleet.Errors;

internal static class FleetErrors
{
    public static Error InvalidOwner => Error.Validation("fleet.invalid_owner", "Owner ID is required.");

    public static Error InvalidPlate => Error.Validation("fleet.invalid_plate", "Plate number cannot be empty.");

    public static Error InvalidSeats => Error.Validation("fleet.invalid_seats", "Seats must be greater than zero.");

    public static Error InvalidLocation => Error.Validation("fleet.invalid_location", "Valid vehicle location is required.");

    public static Error InvalidAddress => Error.Validation("fleet.invalid_address", "Address is required.");

    public static Error InvalidImage => Error.Validation("fleet.invalid_image", "Storage key cannot be empty.");

    public static Error ImageNotFound => Error.NotFound("fleet.image_not_found", "Image not found.");

    public static Error OnlyDraftCanSubmit => Error.Validation("fleet.invalid_state", "Only draft vehicles can be submitted for approval.");

    public static Error MissingPrimaryImage => Error.Validation("fleet.missing_primary_image", "A primary image is required.");

    public static Error InsufficientImages => Error.Validation("fleet.insufficient_images", "At least 3 images are required for approval.");

    public static Error OnlyPendingCanApprove => Error.Validation("fleet.invalid_state", "Only pending approval vehicles can be approved.");

    public static Error OnlyPendingCanReject => Error.Validation("fleet.invalid_state", "Only pending approval vehicles can be rejected.");

    public static Error OnlyPublishedCanUnpublish => Error.Validation("fleet.invalid_state", "Only published vehicles can be unpublished.");

    public static Error OnlyPublishedCanHide => Error.Validation("fleet.invalid_state", "Only published vehicles can be hidden.");

    public static Error OnlyHiddenCanShow => Error.Validation("fleet.invalid_state", "Only hidden vehicles can be shown.");

    public static Error HiddenByAdmin => Error.Validation("fleet.hidden_by_admin", "Vehicle was unpublished by admin and must be resubmitted and re-approved.");

    public static Error OwnerNotFound => Error.NotFound("fleet.owner_not_found", "Owner user was not found.");
    public static Error OwnerRequired => Error.Forbidden("fleet.owner_required", "Only the vehicle owner can manage this vehicle.");

    public static Error PlateDuplicate => Error.Conflict("fleet.plate_duplicate", "Plate number is already registered.");

    public static Error BrandDuplicate => Error.Conflict("fleet.brand_duplicate", "Brand name already exists.");

    public static Error BrandHasModels => Error.Conflict("fleet.brand_in_use", "Brand cannot be deleted while models reference it.");

    public static Error BrandHasVehicles => Error.Conflict("fleet.brand_in_use", "Brand cannot be deleted while vehicles reference it.");

    public static Error ModelDuplicate => Error.Conflict("fleet.model_duplicate", "Model name already exists for this brand.");

    public static Error ModelInUse => Error.Conflict("fleet.model_in_use", "Model cannot be deleted while vehicles reference it.");

    public static Error BrandNotFound => Error.NotFound("fleet.brand_not_found", "Vehicle brand was not found.");

    public static Error ModelNotFound => Error.NotFound("fleet.model_not_found", "Vehicle model was not found.");

}
