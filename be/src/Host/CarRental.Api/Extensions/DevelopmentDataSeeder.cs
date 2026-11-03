using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Fleet.Contracts.Enums;
using CarRental.Modules.Fleet.Data;
using CarRental.Modules.Fleet.Entities;
using CarRental.Modules.Pricing.Data;
using CarRental.Modules.Pricing.Entities;
using CarRental.Modules.Search.Data;
using CarRental.Modules.Search.Entities;
using CarRental.Modules.Users.Data;
using CarRental.Modules.Users.Entities;
using CarRental.Modules.Users.Contracts.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Api.Extensions;

public static class DevelopmentDataSeeder
{
    private const string OwnerEmail = "owner.seed@example.com";
    private const string CustomerEmail = "customer.seed@example.com";
    private const string SeedPassword = "Password123!";
    private static readonly Guid AdminId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static readonly (Guid Id, string Name)[] Brands =
    [
        (Guid.Parse("00000000-0000-0000-0000-000000000101"), "Toyota"),
        (Guid.Parse("00000000-0000-0000-0000-000000000102"), "VinFast")
    ];

    private static readonly (Guid Id, Guid BrandId, string Name)[] Models =
    [
        (Guid.Parse("00000000-0000-0000-0000-000000000201"), Brands[0].Id, "Vios"),
        (Guid.Parse("00000000-0000-0000-0000-000000000202"), Brands[0].Id, "Camry"),
        (Guid.Parse("00000000-0000-0000-0000-000000000203"), Brands[1].Id, "VF 8")
    ];

    public static async Task SeedAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DevelopmentDataSeeder");

        try
        {
            var users = services.GetRequiredService<UsersDbContext>();
            var fleet = services.GetRequiredService<FleetDbContext>();
            var pricing = services.GetRequiredService<PricingDbContext>();
            var search = services.GetRequiredService<SearchDbContext>();
            var passwordHasher = services.GetRequiredService<PasswordHasher<object>>();

            var owner = await SeedOwnerAsync(users, passwordHasher);
            await SeedCustomerAsync(users, passwordHasher);
            await SeedFleetAsync(fleet, pricing, owner.Id.Value);
            await RebuildSearchProjectionAsync(fleet, pricing, search);

            logger.LogInformation(
                "Development data seed completed. Owner login: {OwnerEmail}; customer login: {CustomerEmail}",
                OwnerEmail,
                CustomerEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Development data seed failed.");
            throw;
        }
    }

    private static async Task<User> SeedOwnerAsync(
        UsersDbContext db,
        PasswordHasher<object> passwordHasher)
    {
        var owner = await db.Users
            .FirstOrDefaultAsync(user => user.Email == OwnerEmail);

        if (owner is null)
        {
            var result = User.Register(
                OwnerEmail,
                "0900000101",
                passwordHasher.HashPassword(new object(), SeedPassword));
            if (result.IsFailure)
                throw new InvalidOperationException($"Could not create seed owner: {result.Error.Message}");

            owner = result.Value;
            var profileResult = owner.UpdateProfile("Seed Vehicle Owner", "1 Nguyen Hue, Ho Chi Minh City");
            if (profileResult.IsFailure)
                throw new InvalidOperationException($"Could not create seed owner profile: {profileResult.Error.Message}");

            var onboardingResult = owner.SubmitOwnerOnboarding("SEED-OWNER-001");
            if (onboardingResult.IsFailure)
                throw new InvalidOperationException($"Could not onboard seed owner: {onboardingResult.Error.Message}");

            var approvalResult = owner.ApproveOwner(AdminId);
            if (approvalResult.IsFailure)
                throw new InvalidOperationException($"Could not approve seed owner: {approvalResult.Error.Message}");

            db.Users.Add(owner);
            await db.SaveChangesAsync();
        }

        return owner;
    }

    private static async Task SeedCustomerAsync(
        UsersDbContext db,
        PasswordHasher<object> passwordHasher)
    {
        if (await db.Users.AnyAsync(user => user.Email == CustomerEmail))
            return;

        var result = User.Register(
            CustomerEmail,
            "0900000102",
            passwordHasher.HashPassword(new object(), SeedPassword));
        if (result.IsFailure)
            throw new InvalidOperationException($"Could not create seed customer: {result.Error.Message}");

        var customer = result.Value;
        var profileResult = customer.UpdateProfile("Seed Customer", "2 Le Loi, Ho Chi Minh City");
        if (profileResult.IsFailure)
            throw new InvalidOperationException($"Could not create seed customer profile: {profileResult.Error.Message}");

        db.Users.Add(customer);
        await db.SaveChangesAsync();
    }

    private static async Task SeedFleetAsync(
        FleetDbContext fleet,
        PricingDbContext pricing,
        Guid ownerId)
    {
        foreach (var brand in Brands)
        {
            if (!await fleet.VehicleBrands.AnyAsync(item => item.Id == brand.Id))
                fleet.VehicleBrands.Add(new VehicleBrand(brand.Id, brand.Name));
        }

        foreach (var model in Models)
        {
            if (!await fleet.VehicleModels.AnyAsync(item => item.Id == model.Id))
                fleet.VehicleModels.Add(new VehicleModel(model.Id, model.BrandId, model.Name));
        }

        await fleet.SaveChangesAsync();

        await SeedVehicleAsync(
            fleet,
            pricing,
            ownerId,
            "51A-100.01",
            Brands[0].Id,
            Models[0].Id,
            TransmissionType.Auto,
            5,
            new GeoPoint(10.7769, 106.7009),
            "1 Nguyen Hue, District 1, Ho Chi Minh City",
            550_000,
            650_000);

        await SeedVehicleAsync(
            fleet,
            pricing,
            ownerId,
            "51A-100.02",
            Brands[0].Id,
            Models[1].Id,
            TransmissionType.Auto,
            5,
            new GeoPoint(10.7829, 106.6997),
            "15 Le Thanh Ton, District 1, Ho Chi Minh City",
            900_000,
            1_050_000);

        await SeedVehicleAsync(
            fleet,
            pricing,
            ownerId,
            "51A-100.03",
            Brands[1].Id,
            Models[2].Id,
            TransmissionType.Auto,
            5,
            new GeoPoint(10.8014, 106.6522),
            "208 Nguyen Van Cu, District 5, Ho Chi Minh City",
            1_200_000,
            1_400_000);
    }

    private static async Task RebuildSearchProjectionAsync(
        FleetDbContext fleet,
        PricingDbContext pricing,
        SearchDbContext search)
    {
        var vehicles = await fleet.Vehicles
            .AsNoTracking()
            .Where(vehicle => vehicle.Status == VehicleStatus.Published && !vehicle.HiddenByAdmin)
            .ToListAsync();
        var brandNames = await fleet.VehicleBrands
            .AsNoTracking()
            .ToDictionaryAsync(brand => brand.Id, brand => brand.Name);
        var modelNames = await fleet.VehicleModels
            .AsNoTracking()
            .ToDictionaryAsync(model => model.Id, model => model.Name);
        var pricingPlans = await pricing.PricingPlans
            .AsNoTracking()
            .ToDictionaryAsync(plan => plan.VehicleId);

        foreach (var vehicle in vehicles)
        {
            if (!pricingPlans.TryGetValue(vehicle.Id.Value, out var plan))
                continue;

            var document = await search.VehicleSearchDocuments
                .SingleOrDefaultAsync(item => item.Id == vehicle.Id.Value);
            if (document is null)
            {
                document = VehicleSearchDocument.Create(vehicle.Id.Value);
                search.VehicleSearchDocuments.Add(document);
            }

            var now = DateTimeOffset.UtcNow;
            document.ApplyPublished(
                brandNames[vehicle.BrandId],
                modelNames[vehicle.ModelId],
                vehicle.Seats,
                vehicle.Transmission.ToString(),
                vehicle.Location,
                vehicle.Address,
                now);
            document.ApplyPrice(
                Math.Min(plan.WeekdayRate.Amount, plan.WeekendRate.Amount),
                now);
        }

        await search.SaveChangesAsync();
    }

    private static async Task SeedVehicleAsync(
        FleetDbContext fleet,
        PricingDbContext pricing,
        Guid ownerId,
        string plateNumber,
        Guid brandId,
        Guid modelId,
        TransmissionType transmission,
        int seats,
        GeoPoint location,
        string address,
        long weekdayRate,
        long weekendRate)
    {
        var vehicle = await fleet.Vehicles.FirstOrDefaultAsync(item => item.PlateNumber == plateNumber);
        if (vehicle is null)
        {
            var result = Vehicle.Create(
                ownerId,
                plateNumber,
                brandId,
                modelId,
                transmission,
                seats,
                location,
                address);
            if (result.IsFailure)
                throw new InvalidOperationException($"Could not create seed vehicle {plateNumber}: {result.Error.Message}");

            vehicle = result.Value;
            vehicle.AddImage($"seed/{plateNumber}/front.jpg");
            vehicle.AddImage($"seed/{plateNumber}/interior.jpg");
            vehicle.AddImage($"seed/{plateNumber}/rear.jpg");

            var submitResult = vehicle.SubmitForApproval();
            if (submitResult.IsFailure)
                throw new InvalidOperationException($"Could not submit seed vehicle {plateNumber}: {submitResult.Error.Message}");

            var approveResult = vehicle.Approve(AdminId);
            if (approveResult.IsFailure)
                throw new InvalidOperationException($"Could not approve seed vehicle {plateNumber}: {approveResult.Error.Message}");

            fleet.Vehicles.Add(vehicle);
            await fleet.SaveChangesAsync();
        }

        if (!await pricing.PricingPlans.AnyAsync(plan => plan.VehicleId == vehicle.Id.Value))
        {
            var result = PricingPlan.Create(
                vehicle.Id.Value,
                Money.Of(weekdayRate),
                Money.Of(weekendRate),
                Money.Of(2_000_000),
                300,
                DateTimeOffset.UtcNow,
                Money.Of(80_000));
            if (result.IsFailure)
                throw new InvalidOperationException($"Could not create pricing for seed vehicle {plateNumber}: {result.Error.Message}");

            pricing.PricingPlans.Add(result.Value);
            await pricing.SaveChangesAsync();
        }
    }
}
