using CarRental.Modules.Users.Contracts.Enums;
using CarRental.Modules.Users.Entities;
using FluentAssertions;
using Xunit;

namespace CarRental.Modules.Tests;

public class UsersModuleTests
{
    [Fact]
    public void Register_ShouldInitializeWithCustomerRoleAndActiveStatus()
    {
        var result = User.Register("renter@example.com", "0901234567", "hashed_password");

        result.IsSuccess.Should().BeTrue();
        var user = result.Value;
        user.Email.Should().Be("renter@example.com");
        user.Phone.Should().Be("0901234567");
        user.Role.Should().Be(UserRole.Customer);
        user.Status.Should().Be(UserStatus.Active);
        user.IdentityDocuments.Should().BeEmpty();
    }

    [Fact]
    public void Register_WithInvalidData_ShouldFail()
    {
        var emptyEmail = User.Register("", "0901234567", "pwd");
        emptyEmail.IsSuccess.Should().BeFalse();
        emptyEmail.Error.Code.Should().Be("users.invalid_email");

        var emptyPhone = User.Register("test@test.com", "", "pwd");
        emptyPhone.IsSuccess.Should().BeFalse();
        emptyPhone.Error.Code.Should().Be("users.invalid_phone");

        var emptyPwd = User.Register("test@test.com", "0901234567", "");
        emptyPwd.IsSuccess.Should().BeFalse();
        emptyPwd.Error.Code.Should().Be("users.invalid_password");
    }

    [Fact]
    public void UpdateProfile_ShouldCreateOrUpdateCustomerProfile()
    {
        var user = User.Register("user@test.com", "0901234567", "pwd").Value;

        var result = user.UpdateProfile("Nguyen Van A", "123 Nguyen Hue");
        result.IsSuccess.Should().BeTrue();
        user.CustomerProfile.Should().NotBeNull();
        user.CustomerProfile!.FullName.Should().Be("Nguyen Van A");
        user.CustomerProfile.Address.Should().Be("123 Nguyen Hue");

        user.UpdateProfile("Nguyen Van B", "456 Le Loi");
        user.CustomerProfile.FullName.Should().Be("Nguyen Van B");
    }

    [Fact]
    public void SubmitOwnerOnboarding_And_Approve_ShouldUpgradeRoleToOwner()
    {
        var user = User.Register("owner@test.com", "0901234567", "pwd").Value;

        var submitResult = user.SubmitOwnerOnboarding("0102030405");
        submitResult.IsSuccess.Should().BeTrue();
        user.OwnerProfile.Should().NotBeNull();
        user.OwnerProfile!.TaxCode.Should().Be("0102030405");
        user.OwnerProfile.ApprovalStatus.Should().Be(OwnerApprovalStatus.Pending);

        var adminId = Guid.NewGuid();
        var approveResult = user.ApproveOwner(adminId);
        approveResult.IsSuccess.Should().BeTrue();
        user.Role.Should().Be(UserRole.Owner);
        user.OwnerProfile.ApprovalStatus.Should().Be(OwnerApprovalStatus.Approved);
    }

    [Fact]
    public void RejectOwner_ShouldSetApprovalStatusToRejected()
    {
        var user = User.Register("owner@test.com", "0901234567", "pwd").Value;
        user.SubmitOwnerOnboarding("0102030405");

        var adminId = Guid.NewGuid();
        var rejectResult = user.RejectOwner(adminId, "Invalid tax code");
        rejectResult.IsSuccess.Should().BeTrue();
        user.Role.Should().Be(UserRole.Customer);
        user.OwnerProfile!.ApprovalStatus.Should().Be(OwnerApprovalStatus.Rejected);
        user.OwnerProfile.RejectReason.Should().Be("Invalid tax code");
    }

    [Fact]
    public void GetBookingEligibility_WithoutProfileAndDocs_ShouldBeIneligible()
    {
        var user = User.Register("user@test.com", "0901234567", "pwd").Value;

        var eligibility = user.GetBookingEligibility();
        eligibility.IsEligible.Should().BeFalse();
        eligibility.IneligibilityReason.Should().Contain("Customer profile");
    }

    [Fact]
    public void GetBookingEligibility_WithFullProfileAndVerifiedDocs_ShouldBeEligible()
    {
        var user = User.Register("user@test.com", "0901234567", "pwd").Value;
        user.UpdateProfile("Nguyen Van A", "123 Nguyen Hue");

        user.AddIdentityDocument(DocumentType.CCCD, "keys/cccd.jpg");
        user.AddIdentityDocument(DocumentType.GPLX, "keys/gplx.jpg");

        var adminId = Guid.NewGuid();
        var cccdDoc = user.IdentityDocuments.First(d => d.Type == DocumentType.CCCD);
        var gplxDoc = user.IdentityDocuments.First(d => d.Type == DocumentType.GPLX);

        user.VerifyDocument(cccdDoc.Id, adminId, true);
        user.VerifyDocument(gplxDoc.Id, adminId, true);

        var eligibility = user.GetBookingEligibility();
        eligibility.IsEligible.Should().BeTrue();
        eligibility.IneligibilityReason.Should().BeNull();
    }

    [Fact]
    public void Ban_And_Unban_ShouldTransitionStatus()
    {
        var user = User.Register("user@test.com", "0901234567", "pwd").Value;
        var adminId = Guid.NewGuid();

        user.Ban(adminId, "Fraud");
        user.Status.Should().Be(UserStatus.Banned);

        var eligibility = user.GetBookingEligibility();
        eligibility.IsEligible.Should().BeFalse();
        eligibility.IneligibilityReason.Should().Contain("banned");

        user.Unban(adminId);
        user.Status.Should().Be(UserStatus.Active);
    }
}
