using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Users.Contracts.Dtos;
using CarRental.Modules.Users.Contracts.Events;
using CarRental.Modules.Users.Enums;
using CarRental.Modules.Users.Events;
using CarRental.Modules.Users.ValueObjects;

namespace CarRental.Modules.Users.Entities;

public class UserProfile : AggregateRoot<UserId>
{
    private readonly HashSet<UserRole> _roles = new();

    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public PhoneNumber? PhoneNumber { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public FileReference? Avatar { get; private set; }
    public IReadOnlyCollection<UserRole> Roles => _roles;
    public UserStatus Status { get; private set; }
    public IdentityDocument? IdentityDocument { get; private set; }
    public DriverLicense? DriverLicense { get; private set; }
    public PayoutAccount? PayoutAccount { get; private set; }

    private UserProfile()
    {
    }

    private UserProfile(
        UserId id,
        string email,
        string fullName,
        PhoneNumber? phoneNumber,
        DateOnly? dateOfBirth) : base(id)
    {
        Email = email;
        FullName = fullName;
        PhoneNumber = phoneNumber;
        DateOfBirth = dateOfBirth;
        Status = UserStatus.Active;
        _roles.Add(UserRole.Renter);
    }

    public static Result<UserProfile> Register(
        UserId id,
        string email,
        string fullName,
        PhoneNumber? phoneNumber = null,
        DateOnly? dateOfBirth = null,
        DateTimeOffset? now = null)
    {
        if (id.Value == Guid.Empty)
        {
            return Result<UserProfile>.Failure(new Error("user.invalid_id", "User ID cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return Result<UserProfile>.Failure(new Error("user.invalid_email", "A valid email address is required."));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result<UserProfile>.Failure(new Error("user.invalid_name", "Full name is required."));
        }

        var profile = new UserProfile(id, email.Trim().ToLowerInvariant(), fullName.Trim(), phoneNumber, dateOfBirth);
        var timestamp = now ?? DateTimeOffset.UtcNow;
        profile.Raise(new UserRegistered(Guid.CreateVersion7(), timestamp, id.Value, profile.Email, profile.FullName));

        return Result<UserProfile>.Success(profile);
    }

    public Result UpdateProfile(
        string fullName,
        PhoneNumber? phoneNumber,
        DateOnly? dateOfBirth,
        FileReference? avatar)
    {
        if (Status == UserStatus.Banned)
        {
            return Result.Failure(new Error("user.banned", "Banned users cannot update their profile."));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result.Failure(new Error("user.invalid_name", "Full name cannot be empty."));
        }

        if (avatar != null && avatar.IsPrivate)
        {
            return Result.Failure(new Error("user.avatar_private", "Avatar photo must be public."));
        }

        FullName = fullName.Trim();
        PhoneNumber = phoneNumber;
        DateOfBirth = dateOfBirth;
        Avatar = avatar;

        return Result.Success();
    }

    public Result SubmitIdentityDocument(
        string citizenIdNumber,
        FileReference frontImage,
        FileReference backImage,
        FileReference? selfieImage,
        DateTimeOffset now)
    {
        if (Status != UserStatus.Active)
        {
            return Result.Failure(new Error("user.not_active", "Only active users can submit identity documents."));
        }

        IdentityDocument = IdentityDocument.CreatePending(citizenIdNumber, frontImage, backImage, selfieImage);
        return Result.Success();
    }

    public Result ApproveIdentity(Guid adminId, DateTimeOffset now)
    {
        if (IdentityDocument == null)
        {
            return Result.Failure(new Error("user.identity_not_found", "No identity document found to approve."));
        }

        if (IdentityDocument.Status != DocumentStatus.Pending)
        {
            return Result.Failure(new Error("user.identity_not_pending", "Only pending identity documents can be approved."));
        }

        IdentityDocument.Approve(adminId, now);
        Raise(new IdentityReviewed(Guid.CreateVersion7(), now, Id.Value, true, null));
        return Result.Success();
    }

    public Result RejectIdentity(Guid adminId, string reason, DateTimeOffset now)
    {
        if (IdentityDocument == null)
        {
            return Result.Failure(new Error("user.identity_not_found", "No identity document found to reject."));
        }

        if (IdentityDocument.Status != DocumentStatus.Pending)
        {
            return Result.Failure(new Error("user.identity_not_pending", "Only pending identity documents can be rejected."));
        }

        IdentityDocument.Reject(adminId, reason, now);
        Raise(new IdentityReviewed(Guid.CreateVersion7(), now, Id.Value, false, reason));
        return Result.Success();
    }

    public Result SubmitDriverLicense(
        string number,
        LicenseClass licenseClass,
        DateOnly expiresOn,
        FileReference image,
        DateTimeOffset now)
    {
        if (Status != UserStatus.Active)
        {
            return Result.Failure(new Error("user.not_active", "Only active users can submit driver licenses."));
        }

        DriverLicense = DriverLicense.CreatePending(number, licenseClass, expiresOn, image);
        return Result.Success();
    }

    public Result ApproveDriverLicense(Guid adminId, DateTimeOffset now)
    {
        if (DriverLicense == null)
        {
            return Result.Failure(new Error("user.license_not_found", "No driver license found to approve."));
        }

        if (DriverLicense.Status != DocumentStatus.Pending)
        {
            return Result.Failure(new Error("user.license_not_pending", "Only pending driver licenses can be approved."));
        }

        DriverLicense.Approve(adminId, now);
        Raise(new DriverLicenseReviewed(Guid.CreateVersion7(), now, Id.Value, true, null));
        return Result.Success();
    }

    public Result RejectDriverLicense(Guid adminId, string reason, DateTimeOffset now)
    {
        if (DriverLicense == null)
        {
            return Result.Failure(new Error("user.license_not_found", "No driver license found to reject."));
        }

        if (DriverLicense.Status != DocumentStatus.Pending)
        {
            return Result.Failure(new Error("user.license_not_pending", "Only pending driver licenses can be rejected."));
        }

        DriverLicense.Reject(adminId, reason, now);
        Raise(new DriverLicenseReviewed(Guid.CreateVersion7(), now, Id.Value, false, reason));
        return Result.Success();
    }

    public Result BecomeOwner(PayoutAccount payoutAccount, DateTimeOffset now)
    {
        if (Status != UserStatus.Active)
        {
            return Result.Failure(new Error("user.not_active", "Only active users can become owners."));
        }

        if (IdentityDocument == null || IdentityDocument.Status != DocumentStatus.Verified)
        {
            return Result.Failure(new Error("user.identity_not_verified", "Verified identity document is required to become an owner."));
        }

        ArgumentNullException.ThrowIfNull(payoutAccount);
        PayoutAccount = payoutAccount;
        _roles.Add(UserRole.Owner);

        Raise(new UserBecameOwnerDomainEvent(Id, now));
        return Result.Success();
    }

    public Result SetPayoutAccount(PayoutAccount payoutAccount)
    {
        if (!_roles.Contains(UserRole.Owner))
        {
            return Result.Failure(new Error("user.not_owner", "Only owners can configure payout accounts."));
        }

        ArgumentNullException.ThrowIfNull(payoutAccount);
        PayoutAccount = payoutAccount;
        return Result.Success();
    }

    public Result Suspend(string reason, Guid adminId, DateTimeOffset now)
    {
        if (Status == UserStatus.Banned)
        {
            return Result.Failure(new Error("user.already_banned", "Cannot suspend a banned user."));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(new Error("user.invalid_reason", "Suspension reason is required."));
        }

        Status = UserStatus.Suspended;
        Raise(new UserSuspended(Guid.CreateVersion7(), now, Id.Value, reason.Trim()));
        return Result.Success();
    }

    public Result Reinstate(DateTimeOffset now)
    {
        if (Status != UserStatus.Suspended)
        {
            return Result.Failure(new Error("user.not_suspended", "Only suspended users can be reinstated."));
        }

        Status = UserStatus.Active;
        Raise(new UserReinstatedDomainEvent(Id, now));
        return Result.Success();
    }

    public Result Ban(string reason, Guid adminId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(new Error("user.invalid_reason", "Ban reason is required."));
        }

        Status = UserStatus.Banned;
        Raise(new UserSuspended(Guid.CreateVersion7(), now, Id.Value, $"Banned: {reason.Trim()}"));
        return Result.Success();
    }

    public RenterEligibilityDto GetRenterEligibility(DateTimeOffset now)
    {
        var reasons = new List<string>();

        if (Status != UserStatus.Active)
        {
            reasons.Add($"User account is not active (status: {Status}).");
        }

        if (IdentityDocument == null || IdentityDocument.Status != DocumentStatus.Verified)
        {
            reasons.Add("Citizen identity document is not verified.");
        }

        if (DriverLicense == null || DriverLicense.Status != DocumentStatus.Verified)
        {
            reasons.Add("Driver license is not verified.");
        }
        else
        {
            var today = DateOnly.FromDateTime(now.DateTime);
            if (DriverLicense.ExpiresOn < today)
            {
                reasons.Add("Driver license is expired.");
            }
        }

        if (DateOfBirth == null)
        {
            reasons.Add("Date of birth is required to verify renter age.");
        }
        else
        {
            var today = DateOnly.FromDateTime(now.DateTime);
            var age = today.Year - DateOfBirth.Value.Year;
            if (DateOfBirth.Value > today.AddYears(-age))
            {
                age--;
            }

            if (age < 21)
            {
                reasons.Add("Renter must be at least 21 years old.");
            }
        }

        return new RenterEligibilityDto(reasons.Count == 0, reasons);
    }
}
