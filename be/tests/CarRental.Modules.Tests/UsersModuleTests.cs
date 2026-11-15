using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CarRental.Modules.Users.Contracts.Enums;
using CarRental.Modules.Users.Entities;
using CarRental.Modules.Users.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
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
    public void Register_ShouldTrimAndLowercaseEmail()
    {
        var result = User.Register("  Renter.ABC@EXAMPLE.COM  ", "  0901234567  ", "pwd");

        result.IsSuccess.Should().BeTrue();
        var user = result.Value;
        user.Email.Should().Be("renter.abc@example.com");
        user.Phone.Should().Be("0901234567");
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
    public void UpdateProfile_WhenBanned_ShouldFail()
    {
        var user = User.Register("user@test.com", "0901234567", "pwd").Value;
        user.Ban(Guid.NewGuid(), "Policy violation");

        var result = user.UpdateProfile("Nguyen Van A", "123 Nguyen Hue");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("users.banned");
    }

    [Fact]
    public void UpdateProfile_WithEmptyFullName_ShouldFail()
    {
        var user = User.Register("user@test.com", "0901234567", "pwd").Value;

        var result = user.UpdateProfile("   ", "123 Nguyen Hue");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("users.invalid_fullname");
    }

    [Fact]
    public void AddIdentityDocument_SameType_ShouldReplacePreviousDocument()
    {
        var user = User.Register("user@test.com", "0901234567", "pwd").Value;

        var result1 = user.AddIdentityDocument(DocumentType.CCCD, "keys/old_cccd.jpg");
        result1.IsSuccess.Should().BeTrue();
        user.IdentityDocuments.Should().HaveCount(1);
        user.IdentityDocuments.First().StorageKey.Should().Be("keys/old_cccd.jpg");

        var result2 = user.AddIdentityDocument(DocumentType.CCCD, "keys/new_cccd.jpg");
        result2.IsSuccess.Should().BeTrue();
        user.IdentityDocuments.Should().HaveCount(1);
        user.IdentityDocuments.First().StorageKey.Should().Be("keys/new_cccd.jpg");
    }

    [Fact]
    public void AddIdentityDocument_WhenBanned_ShouldFail()
    {
        var user = User.Register("user@test.com", "0901234567", "pwd").Value;
        user.Ban(Guid.NewGuid(), "Violation");

        var result = user.AddIdentityDocument(DocumentType.CCCD, "keys/doc.jpg");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("users.banned");
    }

    [Fact]
    public void AddIdentityDocument_WithEmptyKey_ShouldFail()
    {
        var user = User.Register("user@test.com", "0901234567", "pwd").Value;

        var result = user.AddIdentityDocument(DocumentType.CCCD, "   ");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("users.invalid_document");
    }

    [Fact]
    public void VerifyDocument_WhenNotFound_ShouldFail()
    {
        var user = User.Register("user@test.com", "0901234567", "pwd").Value;

        var result = user.VerifyDocument(Guid.NewGuid(), Guid.NewGuid(), true);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("users.document_not_found");
    }

    [Fact]
    public void VerifyDocument_WhenRejected_ShouldSetStatusAndReason()
    {
        var user = User.Register("user@test.com", "0901234567", "pwd").Value;
        user.AddIdentityDocument(DocumentType.CCCD, "keys/blurry.jpg");
        var docId = user.IdentityDocuments.First().Id;

        var result = user.VerifyDocument(docId, Guid.NewGuid(), false, "Image is blurry");

        result.IsSuccess.Should().BeTrue();
        var doc = user.IdentityDocuments.First();
        doc.VerificationStatus.Should().Be(DocumentVerificationStatus.Rejected);
        doc.RejectReason.Should().Be("Image is blurry");
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
    public void SubmitOwnerOnboarding_Resubmit_ShouldUpdateTaxCodeAndResetPending()
    {
        var user = User.Register("owner@test.com", "0901234567", "pwd").Value;
        user.SubmitOwnerOnboarding("0102030405");
        user.RejectOwner(Guid.NewGuid(), "Invalid tax code");

        var resubmitResult = user.SubmitOwnerOnboarding("0999999999");

        resubmitResult.IsSuccess.Should().BeTrue();
        user.OwnerProfile!.TaxCode.Should().Be("0999999999");
        user.OwnerProfile.ApprovalStatus.Should().Be(OwnerApprovalStatus.Pending);
        user.OwnerProfile.RejectReason.Should().BeNull();
    }

    [Fact]
    public void SubmitOwnerOnboarding_WhenBanned_ShouldFail()
    {
        var user = User.Register("owner@test.com", "0901234567", "pwd").Value;
        user.Ban(Guid.NewGuid(), "Banned");

        var result = user.SubmitOwnerOnboarding("0102030405");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("users.banned");
    }

    [Fact]
    public void SubmitOwnerOnboarding_WithEmptyTaxCode_ShouldFail()
    {
        var user = User.Register("owner@test.com", "0901234567", "pwd").Value;

        var result = user.SubmitOwnerOnboarding("   ");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("users.invalid_tax_code");
    }

    [Fact]
    public void ApproveOwner_WithoutPendingRequest_ShouldFail()
    {
        var user = User.Register("customer@test.com", "0901234567", "pwd").Value;

        var result = user.ApproveOwner(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("users.no_pending_owner_request");
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
    public void RejectOwner_WithoutPendingRequest_ShouldFail()
    {
        var user = User.Register("customer@test.com", "0901234567", "pwd").Value;

        var result = user.RejectOwner(Guid.NewGuid(), "Reason");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("users.no_pending_owner_request");
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
    public void GetBookingEligibility_MissingCccd_ShouldBeIneligible()
    {
        var user = User.Register("user@test.com", "0901234567", "pwd").Value;
        user.UpdateProfile("Nguyen Van A", "123 Nguyen Hue");
        user.AddIdentityDocument(DocumentType.GPLX, "keys/gplx.jpg");
        user.VerifyDocument(user.IdentityDocuments.First().Id, Guid.NewGuid(), true);

        var eligibility = user.GetBookingEligibility();

        eligibility.IsEligible.Should().BeFalse();
        eligibility.IneligibilityReason.Should().Contain("CCCD");
    }

    [Fact]
    public void GetBookingEligibility_MissingGplx_ShouldBeIneligible()
    {
        var user = User.Register("user@test.com", "0901234567", "pwd").Value;
        user.UpdateProfile("Nguyen Van A", "123 Nguyen Hue");
        user.AddIdentityDocument(DocumentType.CCCD, "keys/cccd.jpg");
        user.VerifyDocument(user.IdentityDocuments.First().Id, Guid.NewGuid(), true);

        var eligibility = user.GetBookingEligibility();

        eligibility.IsEligible.Should().BeFalse();
        eligibility.IneligibilityReason.Should().Contain("GPLX");
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

    [Fact]
    public void VerifyDocumentByTypeMock_ShouldRequireBothIdAndDriverLicenseForIsVerified()
    {
        var user = User.Register("user@test.com", "0901234567", "pwd").Value;
        user.IsIdVerified.Should().BeFalse();
        user.IsDriverLicenseVerified.Should().BeFalse();
        user.IsVerified.Should().BeFalse();

        var verifyIdResult = user.VerifyDocumentByTypeMock(DocumentType.CCCD, "mock/cccd.jpg");
        verifyIdResult.IsSuccess.Should().BeTrue();
        user.IsIdVerified.Should().BeTrue();
        user.IsDriverLicenseVerified.Should().BeFalse();
        user.IsVerified.Should().BeFalse();

        var verifyLicenseResult = user.VerifyDocumentByTypeMock(DocumentType.GPLX, "mock/gplx.jpg");
        verifyLicenseResult.IsSuccess.Should().BeTrue();
        user.IsIdVerified.Should().BeTrue();
        user.IsDriverLicenseVerified.Should().BeTrue();
        user.IsVerified.Should().BeTrue();
        user.IdentityDocuments.Should().HaveCount(2);
    }

    [Fact]
    public void VerifyDocumentByTypeMock_WhenBanned_ShouldFail()
    {
        var user = User.Register("user@test.com", "0901234567", "pwd").Value;
        user.Ban(Guid.NewGuid(), "Violation");

        var result = user.VerifyDocumentByTypeMock(DocumentType.CCCD);
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("users.banned");
    }

    [Fact]
    public void RegisterAdmin_ShouldInitializeWithAdminRoleAndVerifiedStatus()
    {
        var result = User.RegisterAdmin("admin@example.com", "0900000100", "hashed_password");

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(UserRole.Admin);
        result.Value.Status.Should().Be(UserStatus.Active);
        result.Value.IsVerified.Should().BeTrue();
    // =========================================================================
    // RefreshToken Tests
    // =========================================================================
    }
    
    [Fact]
    public void RefreshToken_WhenNotRevokedAndNotExpired_ShouldBeActive()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new RefreshToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hashed_token_value",
            now.AddDays(7),
            "Firefox / macOS",
            now);

        token.IsActive.Should().BeTrue();
        token.RevokedAt.Should().BeNull();
        token.ReplacedByTokenId.Should().BeNull();
        token.DeviceInfo.Should().Be("Firefox / macOS");
    }

    [Fact]
    public void RefreshToken_WhenExpired_ShouldNotBeActive()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new RefreshToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hashed_token_value",
            now.AddMinutes(-5),
            "Chrome / Windows",
            now.AddDays(-7));

        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void RefreshToken_Revoke_ShouldSetRevokedAtAndReplacedByTokenId_AndBecomeInactive()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new RefreshToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hashed_token_value",
            now.AddDays(7),
            "Mobile App",
            now);

        var replacedById = Guid.NewGuid();
        var revokeTime = now.AddHours(2);
        token.Revoke(replacedById, revokeTime);

        token.IsActive.Should().BeFalse();
        token.RevokedAt.Should().Be(revokeTime);
        token.ReplacedByTokenId.Should().Be(replacedById);
    }

    // =========================================================================
    // TokenService Tests
    // =========================================================================

    [Fact]
    public void TokenService_CreateAccessToken_ShouldIncludeExpectedClaims()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "CarRental.TestIssuer",
            Audience = "CarRental.TestAudience",
            SigningKey = "test-secret-key-that-is-at-least-32-bytes-long!",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });

        var tokenService = new TokenService(options);
        var user = User.Register("testjwt@example.com", "0901112222", "pwd").Value;

        var (tokenString, expiresAt) = tokenService.CreateAccessToken(user);

        tokenString.Should().NotBeNullOrWhiteSpace();
        expiresAt.Should().BeAfter(DateTimeOffset.UtcNow);

        var handler = new JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(tokenString);

        parsed.Issuer.Should().Be("CarRental.TestIssuer");
        parsed.Audiences.Should().Contain("CarRental.TestAudience");
        parsed.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.Value.ToString());
        parsed.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == UserRole.Customer.ToString());
        parsed.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == "testjwt@example.com");
    }

    [Fact]
    public void TokenService_CreateRefreshToken_ShouldGenerateTokenAndMatchingHash()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "CarRental.TestIssuer",
            Audience = "CarRental.TestAudience",
            SigningKey = "test-secret-key-that-is-at-least-32-bytes-long!",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 14
        });

        var tokenService = new TokenService(options);

        var (rawToken, hash, expiresAt) = tokenService.CreateRefreshToken();

        rawToken.Should().NotBeNullOrWhiteSpace();
        hash.Should().NotBeNullOrWhiteSpace();
        expiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddDays(13));

        // Verifying hash matches the raw token
        var computedHash = tokenService.HashRefreshToken(rawToken);
        computedHash.Should().Be(hash);
    }

    [Fact]
    public void TokenService_HashRefreshToken_ShouldBeDeterministic()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "CarRental.TestIssuer",
            Audience = "CarRental.TestAudience",
            SigningKey = "test-secret-key-that-is-at-least-32-bytes-long!",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });

        var tokenService = new TokenService(options);

        var hash1 = tokenService.HashRefreshToken("sample-refresh-token");
        var hash2 = tokenService.HashRefreshToken("sample-refresh-token");

        hash1.Should().Be(hash2);
        hash1.Should().MatchRegex("^[A-F0-9]{64}$"); // SHA256 hex uppercase string
    }
}
