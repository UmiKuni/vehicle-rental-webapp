using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Users.Contracts.Dtos;
using CarRental.Modules.Users.Contracts.Enums;

namespace CarRental.Modules.Users.Entities;

public class User : AggregateRoot<UserId>
{
    private readonly List<IdentityDocument> _identityDocuments = new();

    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public UserStatus Status { get; private set; }

    public CustomerProfile? CustomerProfile { get; private set; }
    public OwnerProfile? OwnerProfile { get; private set; }
    public IReadOnlyCollection<IdentityDocument> IdentityDocuments => _identityDocuments.AsReadOnly();

    private User() { }

    private User(UserId id, string email, string phone, string passwordHash)
    {
        Id = id;
        Email = email.Trim().ToLowerInvariant();
        Phone = phone.Trim();
        PasswordHash = passwordHash;
        Role = UserRole.Customer;
        Status = UserStatus.Active;
    }

    public static Result<User> Register(string email, string phone, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<User>(new Error("users.invalid_email", "Email cannot be empty."));

        if (string.IsNullOrWhiteSpace(phone))
            return Result.Failure<User>(new Error("users.invalid_phone", "Phone number cannot be empty."));

        if (string.IsNullOrWhiteSpace(passwordHash))
            return Result.Failure<User>(new Error("users.invalid_password", "Password hash cannot be empty."));

        return Result.Success(new User(UserId.New(), email, phone, passwordHash));
    }

    public Result UpdateProfile(string fullName, string address)
    {
        if (Status == UserStatus.Banned)
            return Result.Failure(new Error("users.banned", "Cannot update profile of a banned user."));

        if (string.IsNullOrWhiteSpace(fullName))
            return Result.Failure(new Error("users.invalid_fullname", "Full name cannot be empty."));

        if (CustomerProfile == null)
        {
            CustomerProfile = new CustomerProfile(Id, fullName.Trim(), address?.Trim() ?? string.Empty);
        }
        else
        {
            CustomerProfile.Update(fullName.Trim(), address?.Trim() ?? string.Empty);
        }

        return Result.Success();
    }

    public Result AddIdentityDocument(DocumentType type, string storageKey)
    {
        if (Status == UserStatus.Banned)
            return Result.Failure(new Error("users.banned", "Cannot submit documents for a banned user."));

        if (string.IsNullOrWhiteSpace(storageKey))
            return Result.Failure(new Error("users.invalid_document", "Storage key cannot be empty."));

        // Replace any existing document of the same type
        _identityDocuments.RemoveAll(d => d.Type == type);
        _identityDocuments.Add(new IdentityDocument(Guid.CreateVersion7(), Id, type, storageKey));

        return Result.Success();
    }

    public Result SubmitOwnerOnboarding(string taxCode, IEnumerable<string>? businessDocKeys = null)
    {
        if (Status == UserStatus.Banned)
            return Result.Failure(new Error("users.banned", "Cannot onboard a banned user as owner."));

        if (string.IsNullOrWhiteSpace(taxCode))
            return Result.Failure(new Error("users.invalid_tax_code", "Tax code is required for owner onboarding."));

        if (OwnerProfile == null)
        {
            OwnerProfile = new OwnerProfile(Id, taxCode.Trim());
        }
        else
        {
            OwnerProfile.Resubmit(taxCode.Trim());
        }

        return Result.Success();
    }

    public Result ApproveOwner(Guid adminId)
    {
        if (Status == UserStatus.Banned)
            return Result.Failure(new Error("users.banned", "Cannot approve owner onboarding for a banned user."));

        if (OwnerProfile == null || OwnerProfile.ApprovalStatus != OwnerApprovalStatus.Pending)
            return Result.Failure(new Error("users.no_pending_owner_request", "No pending owner onboarding request found."));

        OwnerProfile.Approve();
        Role = UserRole.Owner;

        return Result.Success();
    }

    public Result RejectOwner(Guid adminId, string reason)
    {
        if (Status == UserStatus.Banned)
            return Result.Failure(new Error("users.banned", "Cannot approve owner onboarding for a banned user."));

        if (OwnerProfile == null || OwnerProfile.ApprovalStatus != OwnerApprovalStatus.Pending)
            return Result.Failure(new Error("users.no_pending_owner_request", "No pending owner onboarding request found."));

        OwnerProfile.Reject(reason);
        return Result.Success();
    }

    public Result VerifyDocument(Guid docId, Guid adminId, bool ok, string? reason = null)
    {
        var doc = _identityDocuments.FirstOrDefault(d => d.Id == docId);
        if (doc == null)
            return Result.Failure(new Error("users.document_not_found", "Identity document not found."));

        doc.Verify(ok, reason);
        return Result.Success();
    }

    public Result Ban(Guid adminId, string reason)
    {
        Status = UserStatus.Banned;
        return Result.Success();
    }

    public Result Unban(Guid adminId)
    {
        Status = UserStatus.Active;
        return Result.Success();
    }

    public BookingEligibilityDto GetBookingEligibility()
    {
        if (Status == UserStatus.Banned)
            return new BookingEligibilityDto(false, "User account is banned.");

        if (CustomerProfile == null || string.IsNullOrWhiteSpace(CustomerProfile.FullName))
            return new BookingEligibilityDto(false, "Customer profile with full name is required.");

        var hasVerifiedCccd = _identityDocuments.Any(d => d.Type == DocumentType.CCCD && d.VerificationStatus == DocumentVerificationStatus.Verified);
        if (!hasVerifiedCccd)
            return new BookingEligibilityDto(false, "Verified citizen identity document (CCCD) is required.");

        var hasVerifiedGplx = _identityDocuments.Any(d => d.Type == DocumentType.GPLX && d.VerificationStatus == DocumentVerificationStatus.Verified);
        if (!hasVerifiedGplx)
            return new BookingEligibilityDto(false, "Verified driver license (GPLX) is required.");

        return new BookingEligibilityDto(true, null);
    }
}
