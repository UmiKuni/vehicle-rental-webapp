using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Audit.Data;
using CarRental.Modules.Audit.Entities;
using CarRental.Modules.Availability.Contracts.Enums;
using CarRental.Modules.Availability.Data;
using CarRental.Modules.Availability.Entities;
using CarRental.Modules.Bookings.Contracts.Enums;
using CarRental.Modules.Bookings.Data;
using CarRental.Modules.Bookings.Entities;
using CarRental.Modules.Fleet.Contracts.Enums;
using CarRental.Modules.Fleet.Data;
using CarRental.Modules.Fleet.Entities;
using CarRental.Modules.Handover.Contracts.Enums;
using CarRental.Modules.Handover.Data;
using CarRental.Modules.Handover.Entities;
using CarRental.Modules.Notifications.Contracts.Enums;
using CarRental.Modules.Notifications.Data;
using CarRental.Modules.Notifications.Entities;
using CarRental.Modules.Payments.Contracts.Enums;
using CarRental.Modules.Payments.Data;
using CarRental.Modules.Payments.Entities;
using CarRental.Modules.Pricing.Data;
using CarRental.Modules.Pricing.Entities;
using CarRental.Modules.Reporting.Data;
using CarRental.Modules.Reporting.Entities;
using CarRental.Modules.Reviews.Data;
using CarRental.Modules.Reviews.Entities;
using CarRental.Modules.Search.Data;
using CarRental.Modules.Search.Entities;
using CarRental.Modules.Support.Data;
using CarRental.Modules.Support.Entities;
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
    private static readonly Guid SeedBookingId = Guid.Parse("00000000-0000-0000-0000-000000000301");

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
            var bookings = services.GetRequiredService<BookingDbContext>();
            var availability = services.GetRequiredService<AvailabilityDbContext>();
            var payments = services.GetRequiredService<PaymentsDbContext>();
            var handover = services.GetRequiredService<HandoverDbContext>();
            var reviews = services.GetRequiredService<ReviewsDbContext>();
            var support = services.GetRequiredService<SupportDbContext>();
            var notifications = services.GetRequiredService<NotificationsDbContext>();
            var audit = services.GetRequiredService<AuditDbContext>();
            var reporting = services.GetRequiredService<ReportingDbContext>();
            var passwordHasher = services.GetRequiredService<PasswordHasher<object>>();

            var owner = await SeedOwnerAsync(users, passwordHasher);
            var customer = await SeedCustomerAsync(users, passwordHasher);
            await SeedFleetAsync(fleet, pricing, owner.Id.Value);
            await RebuildSearchProjectionAsync(fleet, pricing, search);
            var vehicle = await fleet.Vehicles.OrderBy(item => item.PlateNumber).FirstAsync();
            var booking = await SeedBookingAsync(bookings, owner.Id.Value, customer.Id.Value, vehicle.Id.Value);
            await SeedAvailabilityAsync(availability, booking, vehicle.Id.Value);
            await SeedPaymentsAsync(payments, bookings, booking);
            await SeedHandoverAsync(handover, booking, vehicle.Id.Value, customer.Id.Value, owner.Id.Value);
            await SeedReviewsAsync(reviews, booking, vehicle.Id.Value, customer.Id.Value);
            await SeedSupportAsync(support, booking, customer.Id.Value);
            await SeedNotificationsAsync(notifications, customer.Id.Value);
            await SeedAuditAsync(audit, owner.Id.Value, booking.Id.Value);
            await SeedReportingAsync(reporting, booking);

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

    private static async Task<User> SeedCustomerAsync(
        UsersDbContext db,
        PasswordHasher<object> passwordHasher)
    {
        var existing = await db.Users.FirstOrDefaultAsync(user => user.Email == CustomerEmail);
        if (existing is not null)
            return existing;

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
        return customer;
    }

    private static async Task<Booking> SeedBookingAsync(
        BookingDbContext db,
        Guid ownerId,
        Guid customerId,
        Guid vehicleId)
    {
        var existing = await db.Bookings
            .Include(item => item.LineItems)
            .FirstOrDefaultAsync(item => item.Id == BookingId.From(SeedBookingId));
        if (existing is not null)
            return existing;

        var now = DateTimeOffset.UtcNow;
        var period = new DateTimeRange(now.AddDays(2), now.AddDays(5));
        var policy = new CancellationPolicySnapshot(
            BookingId.From(SeedBookingId),
            """{"freeCancellationHours":48,"penaltyPercent":50}""");
        var lineItems = new List<BookingLineItem>
        {
            new(Guid.CreateVersion7(), BookingId.From(SeedBookingId), BookingLineItemType.Rent, Money.Of(1_950_000)),
            new(Guid.CreateVersion7(), BookingId.From(SeedBookingId), BookingLineItemType.Deposit, Money.Of(2_000_000))
        };

        var result = Booking.Request(
            customerId,
            ownerId,
            vehicleId,
            period,
            "Seed pickup - District 1",
            lineItems,
            policy,
            now,
            BookingId.From(SeedBookingId));
        if (result.IsFailure)
            throw new InvalidOperationException($"Could not create seed booking: {result.Error.Message}");

        var booking = result.Value;
        var approvalResult = booking.Approve(ownerId, now);
        if (approvalResult.IsFailure)
            throw new InvalidOperationException($"Could not approve seed booking: {approvalResult.Error.Message}");

        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking;
    }

    private static async Task SeedAvailabilityAsync(
        AvailabilityDbContext db,
        Booking booking,
        Guid vehicleId)
    {
        var calendar = await db.VehicleCalendars
            .Include(item => item.Entries)
            .FirstOrDefaultAsync(item => item.VehicleId == vehicleId);
        if (calendar is not null)
            return;

        var now = DateTimeOffset.UtcNow;
        calendar = VehicleCalendar.Create(vehicleId, now);
        var result = calendar.PlaceHold(
            booking.Id.Value,
            new DateTimeRange(booking.StartTime, booking.EndTime),
            TimeSpan.FromHours(12),
            now);
        if (result.IsFailure)
            throw new InvalidOperationException($"Could not create seed availability: {result.Error.Message}");
        result = calendar.ConfirmHold(booking.Id.Value, now);
        if (result.IsFailure)
            throw new InvalidOperationException($"Could not confirm seed availability: {result.Error.Message}");

        db.VehicleCalendars.Add(calendar);
        await db.SaveChangesAsync();
    }

    private static async Task SeedPaymentsAsync(
        PaymentsDbContext db,
        BookingDbContext bookingDb,
        Booking booking)
    {
        if (await db.Payments.AnyAsync(item => item.BookingId == booking.Id.Value))
            return;

        var now = DateTimeOffset.UtcNow;
        var paymentResult = Payment.CreatePending(
            booking.Id.Value, Money.Of(1_950_000), Money.Of(2_000_000), now);
        if (paymentResult.IsFailure)
            throw new InvalidOperationException($"Could not create seed payment: {paymentResult.Error.Message}");

        var payment = paymentResult.Value;
        var methodResult = payment.SetPaymentMethod(PaymentMethod.Transfer);
        if (methodResult.IsFailure)
            throw new InvalidOperationException($"Could not set seed payment method: {methodResult.Error.Message}");
        var successResult = payment.MarkSuccess("SEED-TXN-001", now);
        if (successResult.IsFailure)
            throw new InvalidOperationException($"Could not complete seed payment: {successResult.Error.Message}");

        db.Payments.Add(payment);
        var depositResult = Deposit.Hold(booking.Id.Value, Money.Of(2_000_000), now);
        if (depositResult.IsFailure)
            throw new InvalidOperationException($"Could not create seed deposit: {depositResult.Error.Message}");

        db.Deposits.Add(depositResult.Value);
        await db.SaveChangesAsync();
        var bookingResult = booking.MarkPaid(payment.Id.Value, now);
        if (bookingResult.IsFailure)
            throw new InvalidOperationException($"Could not mark seed booking paid: {bookingResult.Error.Message}");
        await bookingDb.SaveChangesAsync();
    }

    private static async Task SeedHandoverAsync(
        HandoverDbContext db,
        Booking booking,
        Guid vehicleId,
        Guid customerId,
        Guid ownerId)
    {
        if (await db.HandoverContexts.AnyAsync(item => item.BookingId == booking.Id.Value))
            return;

        db.HandoverContexts.Add(new HandoverContext(
            booking.Id.Value, vehicleId, customerId, ownerId, booking.StartTime, booking.EndTime));
        await db.SaveChangesAsync();
    }

    private static async Task SeedReviewsAsync(
        ReviewsDbContext db,
        Booking booking,
        Guid vehicleId,
        Guid customerId)
    {
        if (await db.ReviewableBookings.AnyAsync(item => item.BookingId == booking.Id.Value))
            return;

        var reviewable = new ReviewableBooking(
            booking.Id.Value, vehicleId, customerId, DateTimeOffset.UtcNow.AddDays(-1));
        var reviewResult = Review.Create(
            reviewable, customerId, 5, "Seed review: smooth pickup and clean vehicle.", DateTimeOffset.UtcNow);
        if (reviewResult.IsFailure)
            throw new InvalidOperationException($"Could not create seed review: {reviewResult.Error.Message}");

        var summary = RatingSummary.Create(vehicleId);
        summary.AddRating(reviewResult.Value.Rating);
        db.ReviewableBookings.Add(reviewable);
        db.Reviews.Add(reviewResult.Value);
        db.RatingSummaries.Add(summary);
        await db.SaveChangesAsync();
    }

    private static async Task SeedSupportAsync(
        SupportDbContext db,
        Booking booking,
        Guid customerId)
    {
        if (await db.SupportTickets.AnyAsync(item => item.BookingId == booking.Id.Value))
            return;

        var result = SupportTicket.Open(
            booking.Id.Value,
            customerId,
            "Seed support ticket for the sample booking.",
            null,
            DateTimeOffset.UtcNow);
        if (result.IsFailure)
            throw new InvalidOperationException($"Could not create seed support ticket: {result.Error.Message}");

        db.SupportTickets.Add(result.Value);
        await db.SaveChangesAsync();
    }

    private static async Task SeedNotificationsAsync(NotificationsDbContext db, Guid customerId)
    {
        if (await db.Notifications.AnyAsync(item => item.DedupKey == "seed.booking.confirmed"))
            return;

        var result = Notification.Create(
            customerId,
            NotificationType.Email,
            "Your seed booking has been confirmed.",
            "seed.booking.confirmed",
            DateTimeOffset.UtcNow);
        if (result.IsFailure)
            throw new InvalidOperationException($"Could not create seed notification: {result.Error.Message}");

        db.Notifications.Add(result.Value);
        await db.SaveChangesAsync();
    }

    private static async Task SeedAuditAsync(AuditDbContext db, Guid actorId, Guid bookingId)
    {
        if (await db.AuditLogEntries.AnyAsync(item => item.EntityId == bookingId.ToString()))
            return;

        var result = AuditLogEntry.Record(
            Guid.Empty,
            DateTimeOffset.UtcNow,
            actorId,
            "127.0.0.1",
            "SeedBookingCreated",
            "Booking",
            bookingId.ToString(),
            null,
            """{"source":"development-seed"}""");
        if (result.IsFailure)
            throw new InvalidOperationException($"Could not create seed audit entry: {result.Error.Message}");

        db.AuditLogEntries.Add(result.Value);
        await db.SaveChangesAsync();
    }

    private static async Task SeedReportingAsync(ReportingDbContext db, Booking booking)
    {
        var date = DailyBookingStats.ToBucketDate(booking.CreatedAt);
        if (await db.DailyBookingStats.AnyAsync(item => item.Id == date))
            return;

        var stats = DailyBookingStats.Create(date);
        stats.ApplyBookingRequested(booking.CreatedAt);
        stats.ApplyPaymentSucceeded(1_950_000m, booking.CreatedAt);
        db.DailyBookingStats.Add(stats);
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
