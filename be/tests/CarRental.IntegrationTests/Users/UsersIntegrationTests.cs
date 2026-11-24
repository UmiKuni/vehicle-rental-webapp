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
            "SecurePassword123!");

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
            "Password123!");

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
        await Client.PostAsJsonAsync("/api/users/register", new RegisterUserDto(email, "0902223344", password));

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
        await Client.PostAsJsonAsync("/api/users/register", new RegisterUserDto(email, "0903334455", "CorrectPassword123!"));

        var response = await Client.PostAsJsonAsync("/api/users/login", new LoginUserDto(email, "WrongPassword!"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_ValidToken_ShouldRotateRefreshToken()
    {
        var email = "refresh_test@example.com";
        var password = "Password123!";
        var regResponse = await Client.PostAsJsonAsync("/api/users/register", new RegisterUserDto(email, "0904445566", password));
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
    public async Task Logout_ValidToken_ShouldImmediatelyRevokeAccessToken()
    {
        var regResponse = await Client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterUserDto("logout_revoke@example.com", "0906667788", "SecurePassword123!"));
        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var authResult = await regResponse.Content.ReadFromJsonAsync<AuthResultDto>();
        authResult.Should().NotBeNull();

        using var authClient = Factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResult!.AccessToken);

        var meBefore = await authClient.GetAsync("/api/auth/me");
        meBefore.StatusCode.Should().Be(HttpStatusCode.OK);

        var logoutResponse = await authClient.PostAsJsonAsync(
            "/api/auth/logout",
            new RefreshTokenDto(authResult.RefreshToken));
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meAfter = await authClient.GetAsync("/api/auth/me");
        meAfter.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ban_ActiveUser_ShouldImmediatelyInvalidateExistingAccessToken()
    {
        var (customerClient, customerId) = await CreateAuthenticatedCustomerClientAsync(
            "ban_revoke@example.com",
            "0907778899",
            verified: false);

        var meBefore = await customerClient.GetAsync("/api/auth/me");
        meBefore.StatusCode.Should().Be(HttpStatusCode.OK);

        var (adminClient, _) = await CreateAuthenticatedAdminClientAsync(
            "ban_admin@example.com",
            "0908889900");

        var banResponse = await adminClient.PutAsync($"/api/users/{customerId}/ban", content: null);
        banResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meAfter = await customerClient.GetAsync("/api/auth/me");
        meAfter.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_MissingBody_ShouldReturn400BadRequestProblemDetails()
    {
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/api/auth/login", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        body.RootElement.GetProperty("errorCode").GetString().Should().Be("request.missing_body");
    }
}
