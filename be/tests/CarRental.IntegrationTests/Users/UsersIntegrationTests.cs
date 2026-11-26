using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CarRental.IntegrationTests.Fixtures;
using CarRental.Modules.Users.Contracts.Dtos;
using CarRental.Modules.Users.Contracts.Enums;
using CarRental.Modules.Users.Data;
using CarRental.Modules.Users.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarRental.IntegrationTests.Users;

public class UsersIntegrationTests : IntegrationTestBase
{
    public UsersIntegrationTests(CarRentalApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Register_ValidRequest_ShouldCreateUserInDatabaseAndReturn201()
    {
        var request = new RegisterUserDto(
            "renter1@example.com",
            "0901234567",
            "SecurePassword123!",
            "Renter One");

        var response = await Client.PostAsJsonAsync("/api/users/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var authResult = await response.Content.ReadFromJsonAsync<AuthResultDto>();
        authResult.Should().NotBeNull();
        authResult!.AccessToken.Should().NotBeNullOrWhiteSpace();
        authResult.RefreshToken.Should().NotBeNullOrWhiteSpace();

        // Verify persisted in PostgreSQL
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == "renter1@example.com");
        user.Should().NotBeNull();
        user!.Phone.Should().Be("0901234567");
        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ShouldReturn409Conflict()
    {
        var request = new RegisterUserDto(
            "duplicate@example.com",
            "0901111222",
            "Password123!",
            "Duplicate User");

        var firstResponse = await Client.PostAsJsonAsync("/api/users/register", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicateResponse = await Client.PostAsJsonAsync("/api/users/register", request);
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_ValidCredentials_ShouldReturnTokens()
    {
        var email = "login_test@example.com";
        var password = "ValidPassword123!";
        await Client.PostAsJsonAsync("/api/users/register", new RegisterUserDto(email, "0902223344", password, "Login Test"));

        var response = await Client.PostAsJsonAsync("/api/users/login", new LoginUserDto(email, password));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authResult = await response.Content.ReadFromJsonAsync<AuthResultDto>();
        authResult.Should().NotBeNull();
        authResult!.AccessToken.Should().NotBeNullOrWhiteSpace();
        authResult.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_InvalidPassword_ShouldReturn401Unauthorized()
    {
        var email = "wrong_pwd@example.com";
        await Client.PostAsJsonAsync("/api/users/register", new RegisterUserDto(email, "0903334455", "CorrectPassword123!", "Wrong Pwd User"));

        var response = await Client.PostAsJsonAsync("/api/users/login", new LoginUserDto(email, "WrongPassword!"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_ValidToken_ShouldRotateRefreshToken()
    {
        var email = "refresh_test@example.com";
        var password = "Password123!";
        var regResponse = await Client.PostAsJsonAsync("/api/users/register", new RegisterUserDto(email, "0904445566", password, "Refresh Test"));
        var initialAuth = await regResponse.Content.ReadFromJsonAsync<AuthResultDto>();

        var response = await Client.PostAsJsonAsync("/api/users/refresh", new RefreshTokenDto(initialAuth!.RefreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotatedAuth = await response.Content.ReadFromJsonAsync<AuthResultDto>();
        rotatedAuth.Should().NotBeNull();
        rotatedAuth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        rotatedAuth.RefreshToken.Should().NotBeNullOrWhiteSpace();
        rotatedAuth.RefreshToken.Should().NotBe(initialAuth.RefreshToken);
    }

    [Fact]
    public async Task UpdateProfile_AuthenticatedCustomer_ShouldPersistInDatabase()
    {
        var (authClient, userId) = await CreateAuthenticatedCustomerClientAsync("profile_test@example.com", "0905556677");

        var updateDto = new UpdateProfileDto("Nguyen Van Test", "789 Le Duan, District 1, HCMC");
        var response = await authClient.PutAsJsonAsync($"/api/users/{userId}/profile", updateDto);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify database update
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var user = await db.Users.Include(u => u.CustomerProfile).FirstAsync(u => u.Id == UserId.From(userId));
        user.CustomerProfile.Should().NotBeNull();
        user.CustomerProfile!.FullName.Should().Be("Nguyen Van Test");
        user.CustomerProfile.Address.Should().Be("789 Le Duan, District 1, HCMC");
    }

    [Fact]
    public async Task GetMe_UnverifiedCustomer_ShouldReturnDatabaseBackedIdentityAndEmptyProfile()
    {
        var regResponse = await Client.PostAsJsonAsync(
            "/api/users/register",
            new RegisterUserDto("me_unverified@example.com", "0905112233", "SecurePassword123!", ""));
        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var authResult = await regResponse.Content.ReadFromJsonAsync<AuthResultDto>();
        authResult.Should().NotBeNull();

        using var authClient = Factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResult!.AccessToken);

        var meResponse = await authClient.GetAsync("/api/users/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await meResponse.Content.ReadFromJsonAsync<CurrentUserDto>();
        me.Should().NotBeNull();
        me!.UserId.Should().Be(authResult.UserId);
        me.Email.Should().Be("me_unverified@example.com");
        me.Phone.Should().Be("0905112233");
        me.FullName.Should().BeEmpty();
        me.Address.Should().BeEmpty();
        me.Role.Should().Be(UserRole.Customer.ToString());
        me.IsIdVerified.Should().BeFalse();
        me.IsDriverLicenseVerified.Should().BeFalse();
        me.IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task GetMe_AfterProfileUpdate_ShouldReturnLatestProfileFromDatabase()
    {
        var (authClient, userId) = await CreateAuthenticatedCustomerClientAsync("me_profile@example.com", "0905445566");

        var updateDto = new UpdateProfileDto("Nguyen Van Me", "456 Tran Hung Dao, District 5, HCMC");
        var updateResponse = await authClient.PutAsJsonAsync($"/api/users/{userId}/profile", updateDto);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meResponse = await authClient.GetAsync("/api/users/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await meResponse.Content.ReadFromJsonAsync<CurrentUserDto>();

        var profileResponse = await authClient.GetAsync($"/api/users/{userId}/profile");
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await profileResponse.Content.ReadFromJsonAsync<UserProfileDto>();

        me.Should().NotBeNull();
        profile.Should().NotBeNull();
        me!.UserId.Should().Be(profile!.UserId);
        me.Email.Should().Be(profile.Email);
        me.Phone.Should().Be(profile.Phone);
        me.FullName.Should().Be("Nguyen Van Me").And.Be(profile.FullName);
        me.Address.Should().Be("456 Tran Hung Dao, District 5, HCMC").And.Be(profile.Address);
        me.Role.Should().Be(UserRole.Customer.ToString());
        me.IsIdVerified.Should().BeTrue();
        me.IsDriverLicenseVerified.Should().BeTrue();
        me.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Logout_ValidToken_ShouldImmediatelyRevokeAccessToken()
    {
        var regResponse = await Client.PostAsJsonAsync(
            "/api/users/register",
            new RegisterUserDto("logout_revoke@example.com", "0906667788", "SecurePassword123!", "Logout Revoke"));
        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var authResult = await regResponse.Content.ReadFromJsonAsync<AuthResultDto>();
        authResult.Should().NotBeNull();

        using var authClient = Factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResult!.AccessToken);

        var meBefore = await authClient.GetAsync("/api/users/me");
        meBefore.StatusCode.Should().Be(HttpStatusCode.OK);

        var logoutResponse = await authClient.PostAsJsonAsync(
            "/api/users/logout",
            new RefreshTokenDto(authResult.RefreshToken));
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meAfter = await authClient.GetAsync("/api/users/me");
        meAfter.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ban_ActiveUser_ShouldImmediatelyInvalidateExistingAccessToken()
    {
        var (customerClient, customerId) = await CreateAuthenticatedCustomerClientAsync(
            "ban_revoke@example.com",
            "0907778899",
            verified: false);

        var meBefore = await customerClient.GetAsync("/api/users/me");
        meBefore.StatusCode.Should().Be(HttpStatusCode.OK);

        var (adminClient, _) = await CreateAuthenticatedAdminClientAsync(
            "ban_admin@example.com",
            "0908889900");

        var banResponse = await adminClient.PutAsync($"/api/users/{customerId}/ban", content: null);
        banResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meAfter = await customerClient.GetAsync("/api/users/me");
        meAfter.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_MissingBody_ShouldReturn400BadRequestProblemDetails()
    {
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/api/users/login", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        body.RootElement.GetProperty("errorCode").GetString().Should().Be("request.missing_body");
    }
}
