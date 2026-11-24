using CarRental.BuildingBlocks.Results;

namespace CarRental.Modules.Users.Errors;

internal static class UsersErrors
{
    public static Error InvalidEmail => Error.Validation("users.invalid_email", "Email cannot be empty.");

    public static Error InvalidPhone => Error.Validation("users.invalid_phone", "Phone number cannot be empty.");

    public static Error InvalidPassword => Error.Validation("users.invalid_password", "Password cannot be empty.");

    public static Error BannedProfile => Error.Conflict("users.banned", "Cannot update profile of a banned user.");

    public static Error InvalidFullname => Error.Validation("users.invalid_fullname", "Full name cannot be empty.");

    public static Error BannedDocuments => Error.Conflict("users.banned", "Cannot submit documents for a banned user.");

    public static Error InvalidDocument => Error.Validation("users.invalid_document", "Storage key cannot be empty.");

    public static Error BannedOwnerOnboarding => Error.Conflict("users.banned", "Cannot onboard a banned user as owner.");

    public static Error InvalidTaxCode => Error.Validation("users.invalid_tax_code", "Tax code is required for owner onboarding.");

    public static Error BannedOwnerApproval => Error.Conflict("users.banned", "Cannot approve owner onboarding for a banned user.");

    public static Error NoPendingOwnerRequest => Error.Validation("users.no_pending_owner_request", "No pending owner onboarding request found.");

    public static Error DocumentNotFound => Error.NotFound("users.document_not_found", "Identity document not found.");

    public static Error RejectReasonRequired => Error.Validation("users.reject_reason_required", "A reason must be provided when rejecting a document.");

    public static Error InvalidDocumentNumber => Error.Validation("users.invalid_document_number", "National ID (CCCD) number must contain 9 to 12 digits.");

    public static Error InvalidLicenseNumber => Error.Validation("users.invalid_license_number", "Driver's license (GPLX) number must contain 12 digits.");

    public static Error DocumentMissing => Error.Validation("users.document_missing", "Document image must be uploaded before submitting verification.");

    public static Error NotFound => Error.NotFound("users.not_found", "User not found");

    public static Error Duplicate => Error.Conflict("users.duplicate", "Email or phone already in use");

    public static Error CredentialsRequired => Error.Unauthorized("users.invalid_credentials", "Identifier and password required");

    public static Error InvalidCredentials => Error.Unauthorized("users.invalid_credentials", "Invalid credentials");

    public static Error BannedAccount => Error.Conflict("users.banned", "This account is unavailable");

    public static Error RefreshTokenRequired => Error.Validation("users.invalid_refresh_token", "Refresh token is required");

    public static Error RefreshTokenExpired => Error.Validation("users.invalid_refresh_token", "Refresh token is invalid or expired");

    public static Error RefreshTokenInvalid => Error.Validation("users.invalid_refresh_token", "Refresh token is invalid");

}
