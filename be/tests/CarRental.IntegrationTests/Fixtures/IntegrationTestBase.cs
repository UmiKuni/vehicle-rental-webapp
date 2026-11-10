using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using CarRental.Modules.Users.Contracts.Dtos;
using CarRental.Modules.Users.Contracts.Enums;
using CarRental.Modules.Users.Data;
using CarRental.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CarRental.IntegrationTests.Fixtures;

[Collection("IntegrationTests")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly CarRentalApiFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(CarRentalApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    public virtual async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
    }

    public virtual Task DisposeAsync() => Task.CompletedTask;

    protected IServiceScope CreateScope() => Factory.Services.CreateScope();

    protected TDbContext GetDbContext<TDbContext>() where TDbContext : DbContext
    {
        var scope = CreateScope();
        return scope.ServiceProvider.GetRequiredService<TDbContext>();
    }

    protected async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedCustomerClientAsync(
        string email = "customer@example.com",
        string phone = "0901234567",
        string password = "Password123!")
    {
        var registerResponse = await Client.PostAsJsonAsync("/api/auth/register", new RegisterUserDto(
            email,
            phone,
            password));
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginUserDto(
            email,
            password));
        loginResponse.EnsureSuccessStatusCode();

        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResultDto>();

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult!.AccessToken);

        using var scope = CreateScope();
        var usersDb = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var user = await usersDb.Users.FirstAsync(u => u.Email == email.ToLowerInvariant().Trim());

        return (client, user.Id.Value);
    }

    protected async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedOwnerClientAsync(
        string email = "owner@example.com",
        string phone = "0909888777",
        string password = "Password123!",
        string taxCode = "0102030405")
    {
        var (_, userId) = await CreateAuthenticatedCustomerClientAsync(email, phone, password);

        // Transition user to Owner in the database
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var trackedUser = await db.Users.FirstAsync(u => u.Id == UserId.From(userId));
            trackedUser.SubmitOwnerOnboarding(taxCode);
            trackedUser.ApproveOwner(Guid.NewGuid());
            await db.SaveChangesAsync();
        }

        // Re-authenticate to issue JWT with Owner role claim
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginUserDto(
            email,
            password));
        loginResponse.EnsureSuccessStatusCode();
        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResultDto>();

        var ownerClient = Factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult!.AccessToken);

        return (ownerClient, userId);
    }

    protected async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedAdminClientAsync(
        string email = "admin@example.com",
        string phone = "0905554433",
        string password = "AdminPassword123!")
    {
        var (_, userId) = await CreateAuthenticatedCustomerClientAsync(email, phone, password);

        // Manually elevate to Admin in DB
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE users.users SET role = {0} WHERE id = {1}",
                (int)UserRole.Admin,
                userId);
        }

        // Re-authenticate to issue JWT with Admin role claim
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginUserDto(
            email,
            password));
        loginResponse.EnsureSuccessStatusCode();
        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResultDto>();

        var adminClient = Factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult!.AccessToken);

        return (adminClient, userId);
    }
}
