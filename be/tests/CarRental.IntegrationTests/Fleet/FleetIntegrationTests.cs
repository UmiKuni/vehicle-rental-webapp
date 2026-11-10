using System.Net;
using System.Net.Http.Json;
using CarRental.IntegrationTests.Fixtures;
using CarRental.Modules.Fleet.Contracts.Dtos;
using CarRental.Modules.Fleet.Contracts.Enums;
using CarRental.Modules.Fleet.Data;
using CarRental.Modules.Fleet.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarRental.IntegrationTests.Fleet;

public class FleetIntegrationTests : IntegrationTestBase
{
    public FleetIntegrationTests(CarRentalApiFactory factory) : base(factory)
    {
    }

    private async Task<(Guid BrandId, Guid ModelId)> SeedBrandAndModelAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        var brand = new VehicleBrand(Guid.NewGuid(), "Toyota");
        var model = new VehicleModel(Guid.NewGuid(), brand.Id, "Camry");

        db.VehicleBrands.Add(brand);
        db.VehicleModels.Add(model);
        await db.SaveChangesAsync();

        return (brand.Id, model.Id);
    }

    [Fact]
    public async Task CreateVehicle_Unauthenticated_ShouldReturn401()
    {
        var (brandId, modelId) = await SeedBrandAndModelAsync();
        var request = new CreateVehicleDto(
            Guid.NewGuid(),
            "51A-123.45",
            brandId,
            modelId,
            TransmissionType.Auto,
            5,
            10.7769,
            106.7009,
            "District 1, HCMC");

        var response = await Client.PostAsJsonAsync("/api/fleet/vehicles", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateVehicle_AuthenticatedOwner_ShouldPersistDraftVehicle()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedOwnerClientAsync("owner_fleet@example.com", "0908889999");
        var (brandId, modelId) = await SeedBrandAndModelAsync();

        var request = new CreateVehicleDto(
            ownerId,
            "51K-888.99",
            brandId,
            modelId,
            TransmissionType.Auto,
            5,
            10.7769,
            106.7009,
            "Ben Nghe, District 1, HCMC");

        var response = await ownerClient.PostAsJsonAsync("/api/fleet/vehicles", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var summary = await response.Content.ReadFromJsonAsync<VehicleSummaryDto>();
        summary.Should().NotBeNull();
        summary!.PlateNumber.Should().Be("51K-888.99");
        summary.Status.Should().Be(nameof(VehicleStatus.Draft));

        // Verify in PostgreSQL database
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.PlateNumber == "51K-888.99");
        vehicle.Should().NotBeNull();
        vehicle!.OwnerId.Should().Be(ownerId);
        vehicle.Location.Latitude.Should().Be(10.7769);
        vehicle.Location.Longitude.Should().Be(106.7009);
        vehicle.Status.Should().Be(VehicleStatus.Draft);
    }

    [Fact]
    public async Task SubmitVehicle_WithFewerThan3Images_ShouldReturn400BadRequest()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedOwnerClientAsync("owner_submit_fail@example.com", "0907778888");
        var (brandId, modelId) = await SeedBrandAndModelAsync();

        var createResponse = await ownerClient.PostAsJsonAsync("/api/fleet/vehicles", new CreateVehicleDto(
            ownerId,
            "51B-111.22",
            brandId,
            modelId,
            TransmissionType.Auto,
            5,
            10.7769,
            106.7009,
            "District 1, HCMC"));
        var summary = await createResponse.Content.ReadFromJsonAsync<VehicleSummaryDto>();

        var submitResponse = await ownerClient.PostAsync($"/api/fleet/vehicles/{summary!.VehicleId}/submit", null);

        submitResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ApproveVehicle_ByAdmin_ShouldTransitionToPublished()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedOwnerClientAsync("owner_approve@example.com", "0906665555");
        var (adminClient, _) = await CreateAuthenticatedAdminClientAsync("admin_fleet@example.com", "0901119999");
        var (brandId, modelId) = await SeedBrandAndModelAsync();

        // 1. Owner creates vehicle
        var createResponse = await ownerClient.PostAsJsonAsync("/api/fleet/vehicles", new CreateVehicleDto(
            ownerId,
            "51C-777.88",
            brandId,
            modelId,
            TransmissionType.Auto,
            5,
            10.7769,
            106.7009,
            "District 1, HCMC"));
        var summary = await createResponse.Content.ReadFromJsonAsync<VehicleSummaryDto>();
        var vehicleId = summary!.VehicleId;

        // 2. Add 3 images directly to vehicle entity in DB for test preparation
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            var vehicle = await db.Vehicles.FirstAsync(v => v.Id == VehicleId.From(vehicleId));
            vehicle.AddImage("vehicles/img1.jpg");
            vehicle.AddImage("vehicles/img2.jpg");
            vehicle.AddImage("vehicles/img3.jpg");
            vehicle.SubmitForApproval();
            await db.SaveChangesAsync();
        }

        // 3. Admin approves
        var approveResponse = await adminClient.PostAsync($"/api/fleet/vehicles/{vehicleId}/approve", null);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 4. Verify in PostgreSQL
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            var vehicle = await db.Vehicles.FirstAsync(v => v.Id == VehicleId.From(vehicleId));
            vehicle.Status.Should().Be(VehicleStatus.Published);
            vehicle.Approvals.Should().HaveCount(1);
            vehicle.Approvals.First().Decision.Should().Be(ApprovalDecision.Approved);
        }
    }
}
