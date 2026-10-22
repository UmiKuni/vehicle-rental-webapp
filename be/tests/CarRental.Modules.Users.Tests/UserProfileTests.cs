using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Users.Contracts.Events;
using CarRental.Modules.Users.Entities;
using CarRental.Modules.Users.Enums;
using CarRental.Modules.Users.ValueObjects;

namespace CarRental.Modules.Users.Tests;

public class UserProfileTests
{
    private readonly DateTimeOffset _now = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

    private static FileReference CreatePrivateFile(string key) =>
        new(key, null, isPrivate: true, "image/jpeg", 1024);

    [Fact]
    public void Register_ShouldInitializeWithRenterRoleAndActiveStatus()
    {
        var userId = UserId.New();
        var result = UserProfile.Register(userId, "renter@example.com", "Nguyen Van A", null, new DateOnly(1995, 5, 20), _now);

        result.IsSuccess.Should().BeTrue();
        var profile = result.Value;
        profile.Id.Should().Be(userId);
        profile.Email.Should().Be("renter@example.com");
        profile.FullName.Should().Be("Nguyen Van A");
        profile.Status.Should().Be(UserStatus.Active);
        profile.Roles.Should().ContainSingle(r => r == UserRole.Renter);
        profile.DomainEvents.Should().ContainSingle(e => e is UserRegistered);
    }

    [Fact]
    public void BecomeOwner_WithoutVerifiedIdentity_ShouldFail()
    {
        var profile = UserProfile.Register(UserId.New(), "owner@example.com", "Tran Van B", null, new DateOnly(1990, 1, 1), _now).Value;
        var payout = new PayoutAccount("Vietcombank", "1234567890", "Tran Van B");

        var result = profile.BecomeOwner(payout, _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("user.identity_not_verified");
        profile.Roles.Should().NotContain(UserRole.Owner);
    }

    [Fact]
    public void BecomeOwner_WithVerifiedIdentity_ShouldSucceed()
    {
        var profile = UserProfile.Register(UserId.New(), "owner@example.com", "Tran Van B", null, new DateOnly(1990, 1, 1), _now).Value;
        profile.SubmitIdentityDocument("001200001234", CreatePrivateFile("front.jpg"), CreatePrivateFile("back.jpg"), null, _now);
        profile.ApproveIdentity(Guid.NewGuid(), _now);

        var payout = new PayoutAccount("Vietcombank", "1234567890", "Tran Van B");
        var result = profile.BecomeOwner(payout, _now);

        result.IsSuccess.Should().BeTrue();
        profile.Roles.Should().Contain(UserRole.Owner);
        profile.PayoutAccount.Should().Be(payout);
    }

    [Fact]
    public void GetRenterEligibility_WithExpiredLicense_ShouldReturnIneligible()
    {
        var profile = UserProfile.Register(UserId.New(), "renter@example.com", "Le Van C", null, new DateOnly(1995, 1, 1), _now).Value;
        profile.SubmitIdentityDocument("001200001234", CreatePrivateFile("front.jpg"), CreatePrivateFile("back.jpg"), null, _now);
        profile.ApproveIdentity(Guid.NewGuid(), _now);

        // Expired yesterday
        var yesterday = DateOnly.FromDateTime(_now.AddDays(-1).DateTime);
        profile.SubmitDriverLicense("B2-99999", LicenseClass.B2, yesterday, CreatePrivateFile("license.jpg"), _now);
        profile.ApproveDriverLicense(Guid.NewGuid(), _now);

        var eligibility = profile.GetRenterEligibility(_now);

        eligibility.IsEligible.Should().BeFalse();
        eligibility.IneligibilityReasons.Should().Contain("Driver license is expired.");
    }

    [Fact]
    public void GetRenterEligibility_Under21_ShouldReturnIneligible()
    {
        // 20 years old
        var dob = DateOnly.FromDateTime(_now.AddYears(-20).DateTime);
        var profile = UserProfile.Register(UserId.New(), "renter@example.com", "Pham Van D", null, dob, _now).Value;
        profile.SubmitIdentityDocument("001200001234", CreatePrivateFile("front.jpg"), CreatePrivateFile("back.jpg"), null, _now);
        profile.ApproveIdentity(Guid.NewGuid(), _now);

        var futureExpiry = DateOnly.FromDateTime(_now.AddYears(5).DateTime);
        profile.SubmitDriverLicense("B2-99999", LicenseClass.B2, futureExpiry, CreatePrivateFile("license.jpg"), _now);
        profile.ApproveDriverLicense(Guid.NewGuid(), _now);

        var eligibility = profile.GetRenterEligibility(_now);

        eligibility.IsEligible.Should().BeFalse();
        eligibility.IneligibilityReasons.Should().Contain("Renter must be at least 21 years old.");
    }

    [Fact]
    public void GetRenterEligibility_WhenAllConditionsMet_ShouldReturnEligible()
    {
        var dob = new DateOnly(2000, 1, 1); // 26 years old
        var profile = UserProfile.Register(UserId.New(), "renter@example.com", "Pham Van D", null, dob, _now).Value;
        profile.SubmitIdentityDocument("001200001234", CreatePrivateFile("front.jpg"), CreatePrivateFile("back.jpg"), null, _now);
        profile.ApproveIdentity(Guid.NewGuid(), _now);

        var futureExpiry = DateOnly.FromDateTime(_now.AddYears(5).DateTime);
        profile.SubmitDriverLicense("B2-99999", LicenseClass.B2, futureExpiry, CreatePrivateFile("license.jpg"), _now);
        profile.ApproveDriverLicense(Guid.NewGuid(), _now);

        var eligibility = profile.GetRenterEligibility(_now);

        eligibility.IsEligible.Should().BeTrue();
        eligibility.IneligibilityReasons.Should().BeEmpty();
    }

    [Fact]
    public void SubmitIdentityDocument_WhenResubmitted_ShouldResetToPending()
    {
        var profile = UserProfile.Register(UserId.New(), "user@example.com", "Nguyen E", null, new DateOnly(1992, 1, 1), _now).Value;
        profile.SubmitIdentityDocument("001200001234", CreatePrivateFile("front.jpg"), CreatePrivateFile("back.jpg"), null, _now);
        profile.ApproveIdentity(Guid.NewGuid(), _now);
        profile.IdentityDocument!.Status.Should().Be(DocumentStatus.Verified);

        // Re-submit
        profile.SubmitIdentityDocument("001200001234", CreatePrivateFile("front2.jpg"), CreatePrivateFile("back2.jpg"), null, _now.AddDays(1));
        profile.IdentityDocument.Status.Should().Be(DocumentStatus.Pending);
    }

    [Fact]
    public void Suspend_WhenActive_ShouldSetSuspendedAndRaiseEvent()
    {
        var profile = UserProfile.Register(UserId.New(), "user@example.com", "Nguyen F", null, null, _now).Value;
        var adminId = Guid.NewGuid();

        var result = profile.Suspend("Terms violation", adminId, _now);

        result.IsSuccess.Should().BeTrue();
        profile.Status.Should().Be(UserStatus.Suspended);
        profile.DomainEvents.Should().ContainSingle(e => e is UserSuspended);
    }

    [Fact]
    public void Reinstate_WhenSuspended_ShouldSetActive()
    {
        var profile = UserProfile.Register(UserId.New(), "user@example.com", "Nguyen G", null, null, _now).Value;
        profile.Suspend("Investigation", Guid.NewGuid(), _now);
        profile.Status.Should().Be(UserStatus.Suspended);

        var result = profile.Reinstate(_now);

        result.IsSuccess.Should().BeTrue();
        profile.Status.Should().Be(UserStatus.Active);
    }
}
