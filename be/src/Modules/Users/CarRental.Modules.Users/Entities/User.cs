using CarRental.Modules.Users.Errors;
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

    public bool IsIdVerified => _identityDocuments.Any(
        d => d.Type == DocumentType.CCCD && d.VerificationStatus == DocumentVerificationStatus.Verified);

    public bool IsDriverLicenseVerified => _identityDocuments.Any(
        d => d.Type == DocumentType.GPLX && d.VerificationStatus == DocumentVerificationStatus.Verified);

    public bool IsVerified => Role == UserRole.Admin || (IsIdVerified && IsDriverLicenseVerified);

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
            return Result.Failure<User>(UsersErrors.InvalidEmail2D5974);

        if (string.IsNullOrWhiteSpace(phone))
            return Result.Failure<User>(UsersErrors.InvalidPhoneA912AF);

        if (string.IsNullOrWhiteSpace(passwordHash))
            return Result.Failure<User>(UsersErrors.InvalidPassword87B027);

        return Result.Success(new User(UserId.New(), email, phone, passwordHash));
    }

    public static Result<User> RegisterAdmin(string email, string phone, string passwordHash)
    {
        var result = Register(email, phone, passwordHash);
        if (result.IsFailure)
            return result;

        result.Value.Role = UserRole.Admin;
        return result;
    }

    public Result UpdateProfile(string fullName, string address)
    {
        if (Status == UserStatus.Banned)
            return Result.Failure(UsersErrors.Banned152FB7);

        if (string.IsNullOrWhiteSpace(fullName))
            return Result.Failure(UsersErrors.InvalidFullname);

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
            return Result.Failure(UsersErrors.Banned3E03B5);

        if (string.IsNullOrWhiteSpace(storageKey))
            return Result.Failure(UsersErrors.InvalidDocument);

        var existing = _identityDocuments.FirstOrDefault(d => d.Type == type);
        if (existing is null)
        {
            _identityDocuments.Add(new IdentityDocument(Guid.CreateVersion7(), Id, type, storageKey));
        }
        else
        {
            existing.UpdateStorageKey(storageKey);
        }

        return Result.Success();
    }

    public Result VerifyDocumentByTypeMock(DocumentType type, string? storageKey = null)
    {
        if (Status == UserStatus.Banned)
            return Result.Failure(UsersErrors.Banned3E03B5);

        var effectiveKey = string.IsNullOrWhiteSpace(storageKey)
            ? $"mock/{type.ToString().ToLowerInvariant()}/{Id.Value:N}.jpg"
            : storageKey.Trim();

        var existing = _identityDocuments.FirstOrDefault(d => d.Type == type);
        if (existing is null)
        {
            var doc = new IdentityDocument(Guid.CreateVersion7(), Id, type, effectiveKey);
            doc.Verify(true, null);
            _identityDocuments.Add(doc);
        }
        else
        {
            existing.UpdateStorageKey(effectiveKey);
            existing.Verify(true, null);
        }

        return Result.Success();
    }

    public Result SubmitOwnerOnboarding(string taxCode, IEnumerable<string>? businessDocKeys = null)
    {
        if (Status == UserStatus.Banned)
            return Result.Failure(UsersErrors.Banned7B690E);

        if (string.IsNullOrWhiteSpace(taxCode))
            return Result.Failure(UsersErrors.InvalidTaxCode);

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
            return Result.Failure(UsersErrors.Banned29AEB1);

        if (OwnerProfile == null || OwnerProfile.ApprovalStatus != OwnerApprovalStatus.Pending)
            return Result.Failure(UsersErrors.NoPendingOwnerRequest);

        OwnerProfile.Approve();
        Role = UserRole.Owner;

        return Result.Success();
    }

    public Result RejectOwner(Guid adminId, string reason)
    {
        if (Status == UserStatus.Banned)
            return Result.Failure(UsersErrors.Banned29AEB1);

        if (OwnerProfile == null || OwnerProfile.ApprovalStatus != OwnerApprovalStatus.Pending)
            return Result.Failure(UsersErrors.NoPendingOwnerRequest);

        OwnerProfile.Reject(reason);
        return Result.Success();
    }

    public Result VerifyDocument(Guid docId, Guid adminId, bool ok, string? reason = null)
    {
        var doc = _identityDocuments.FirstOrDefault(d => d.Id == docId);
        if (doc == null)
            return Result.Failure(UsersErrors.DocumentNotFound);

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
