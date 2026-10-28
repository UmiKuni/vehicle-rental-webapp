using CarRental.BuildingBlocks.Results;

namespace CarRental.Modules.Users.Errors;

internal static class UsersErrors
{
    public static Error InvalidEmail2D5974 => Error.Validation("users.invalid_email", "Email cannot be empty.");

    public static Error InvalidPhoneA912AF => Error.Validation("users.invalid_phone", "Phone number cannot be empty.");

    public static Error InvalidPassword87B027 => Error.Validation("users.invalid_password", "Password hash cannot be empty.");

    public static Error Banned152FB7 => Error.Conflict("users.banned", "Cannot update profile of a banned user.");

    public static Error InvalidFullname => Error.Validation("users.invalid_fullname", "Full name cannot be empty.");

    public static Error Banned3E03B5 => Error.Conflict("users.banned", "Cannot submit documents for a banned user.");

    public static Error InvalidDocument => Error.Validation("users.invalid_document", "Storage key cannot be empty.");

    public static Error Banned7B690E => Error.Conflict("users.banned", "Cannot onboard a banned user as owner.");

    public static Error InvalidTaxCode => Error.Validation("users.invalid_tax_code", "Tax code is required for owner onboarding.");

    public static Error Banned29AEB1 => Error.Conflict("users.banned", "Cannot approve owner onboarding for a banned user.");

    public static Error NoPendingOwnerRequest => Error.Validation("users.no_pending_owner_request", "No pending owner onboarding request found.");

    public static Error DocumentNotFound => Error.NotFound("users.document_not_found", "Identity document not found.");

    public static Error NotFound => Error.NotFound("users.not_found", "User not found");

    public static Error InvalidEmail4D9514 => Error.Validation("users.invalid_email", "Email cannot be empty");

    public static Error InvalidPhone8199CF => Error.Validation("users.invalid_phone", "Phone cannot be empty");

    public static Error InvalidPassword4BB6F3 => Error.Validation("users.invalid_password", "Password cannot be empty");

    public static Error Duplicate => Error.Conflict("users.duplicate", "Email or phone already in use");

    public static Error InvalidCredentials2B2E58 => Error.Unauthorized("users.invalid_credentials", "Identifier and password required");

    public static Error InvalidCredentialsE41784 => Error.Unauthorized("users.invalid_credentials", "Invalid credentials");

    public static Error Banned3C3263 => Error.Conflict("users.banned", "This account is unavailable");

    public static Error InvalidRefreshToken7F376E => Error.Validation("users.invalid_refresh_token", "Refresh token is required");

    public static Error InvalidRefreshToken339042 => Error.Validation("users.invalid_refresh_token", "Refresh token is invalid or expired");

    public static Error InvalidRefreshToken0C6350 => Error.Validation("users.invalid_refresh_token", "Refresh token is invalid");

}
