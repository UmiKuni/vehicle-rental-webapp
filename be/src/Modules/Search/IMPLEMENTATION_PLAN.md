# Search Module — Implementation Plan

> **Module Directory:** `src/Modules/Search`  
> **Projects:** `CarRental.Modules.Search.csproj`, `CarRental.Modules.Search.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-search`  
> **Phase:** 4 (Projections & Platform — Parallel Stream 1)  
> **PostgreSQL Schema:** `search`  
> **References:** [be/docs/domain-model-entities-updated.md](../../../docs/domain-model-entities-updated.md) (Section 4.8 & 5)

---

## 1. Objectives & Scope
The Search module provides a high-performance, denormalized read model for vehicle discovery. It supports geospatial queries (`ST_DWithin` via PostGIS), dynamic faceted filtering (brand, price, seats, transmission, features), and checks real-time availability for top search candidates via `IAvailabilityApi`. It also manages user favorites.

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after Phase 1 & 2 contracts are available
git checkout -b feat/module-search

# Build module
dotnet build src/Modules/Search/CarRental.Modules.Search/CarRental.Modules.Search.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks` (GeoPoint, Money, Result, Guard).
- **Synchronous Upstream Contracts:**
  - `IAvailabilityApi.AreAvailableAsync` (`Availability.Contracts`) — bulk check for candidate availability.
- **Consumes Events from:**
  - `Fleet`: `VehicleApproved`, `VehicleListingChanged`, `VehicleStatusChanged`.
  - `Pricing`: `PricingPlanChanged`.
  - `Reviews`: `ReviewPublished`, `ReviewHidden`.
  - `Bookings`: `TripCompleted`.

---

## 4. Contracts Project (`CarRental.Modules.Search.Contracts`)
### 4.1 Synchronous Interface (`ISearchApi`)
```csharp
public interface ISearchApi
{
    Task<Result<PagedResult<SearchVehicleDto>>> SearchVehiclesAsync(VehicleSearchQuery query, CancellationToken ct = default);
}
```

### 4.2 DTOs & Query Models
- `VehicleSearchQuery(double? Latitude, double? Longitude, double? RadiusKm, DateTimeRange? DesiredPeriod, Money? MinPrice, Money? MaxPrice, string? Brand, int? Seats, string? Transmission, IReadOnlyList<string>? Features, int Page, int PageSize)`
- `SearchVehicleDto(Guid VehicleId, string Title, string Brand, string Model, int Year, Money BaseDailyRate, double? DistanceKm, double AverageRating, int ReviewCount, string? PrimaryPhotoUrl, string City, string District)`

---

## 5. Domain & Read Models (`Entities/`, `Projections/`)

### 5.1 Read Model: `SearchableVehicle`
- Properties:
  - `Guid VehicleId` (Primary Key)
  - `Guid OwnerId`
  - `string Title`, `string Brand`, `string Model`, `int Year`
  - `int Seats`, `string Transmission`, `string FuelType`
  - `List<string> Features`
  - `Point Location` (NetTopologySuite PostGIS Point, SRID 4326)
  - `string City`, `string District`
  - `string? PrimaryPhotoUrl`
  - `Money? BaseDailyRate`
  - `decimal AverageRating`, `int ReviewCount`, `int TripCount`
  - `bool IsListed` (Listing rule: `true` only when vehicle is `Active` AND a pricing plan exists)
  - `uint SourceVersion` (to guarantee idempotent, out-of-order event resilience)
- Mutation Methods:
  - `void ApplyListing(VehicleListingChanged e)`
  - `void ApplyPricing(PricingPlanChanged e)`
  - `void ApplyStatus(VehicleStatusChanged e)` (Delists when Paused/Archived, re-lists when Active)
  - `void ApplyRating(ReviewPublished e)`
  - `void ApplyReviewHidden(ReviewHidden e)`
  - `void ApplyTripCompleted(TripCompleted e)`
  - `void Delist()`

### 5.2 Aggregate Root: `Favorite`
- Properties: `Guid UserId`, `Guid VehicleId`, `DateTimeOffset AddedAt`.
- Methods: `Add(userId, vehicleId)`, `Remove()`.

---

## 6. Persistence & EF Core (`Data/`)
- `SearchDbContext`: `HasDefaultSchema("search")`.
- `IEntityTypeConfiguration<SearchableVehicle>`:
  - Spatial index on `Location`:
    ```csharp
    builder.HasIndex(v => v.Location).HasMethod("GIST");
    ```
  - Indexes on `(City, District)`, `BaseDailyRate`, `IsListed`.
- `IEntityTypeConfiguration<Favorite>`:
  - Composite primary key on `(UserId, VehicleId)`.
- Migration: `AddSearchInitialMigration`.

---

## 7. Event Handlers & Query Pipeline (`EventHandlers/`, `Services/`)
- Wolverine Event Projections:
  - `VehicleApprovedHandler`, `VehicleListingChangedHandler`, `VehiclePausedHandler`
  - `PricingPlanChangedHandler`
  - `ReviewPublishedHandler`
- Two-Stage Search Algorithm:
  1. **Stage 1 (PostGIS Candidate Retrieval):** Fetch top `N * 2` vehicles matching location (`ST_DWithin`), price, and filters from `SearchableVehicle`.
  2. **Stage 2 (Availability Filter):** If `DesiredPeriod` is provided, call `IAvailabilityApi.AreAvailableAsync(candidateIds, period)` to prune unavailable slots.
  3. **Stage 3 (Paginate & Return):** Return final page of results.

---

## 8. Step-by-Step Task Checklist
- [ ] Implement `SearchableVehicle` read model and `Favorite` aggregate.
- [ ] Setup `SearchDbContext` with NetTopologySuite PostGIS configuration.
- [ ] Write EF Core migration with spatial GIST index.
- [ ] Implement Wolverine idempotent event projection handlers.
- [ ] Implement two-stage query service with `IAvailabilityApi`.
- [ ] Build Minimal API endpoints (`GET /api/search`, `POST /api/favorites/{vehicleId}`).
- [ ] Unit tests for `SearchableVehicle` event mutation methods.
- [ ] Integration tests using Testcontainers PostgreSQL + PostGIS:
  - Spatial distance filtering.
  - Candidate pruning via mocked availability.
