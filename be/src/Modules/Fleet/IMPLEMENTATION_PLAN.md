# Fleet Module — Implementation Plan

> **Module Directory:** `src/Modules/Fleet`  
> **Projects:** `CarRental.Modules.Fleet.csproj`, `CarRental.Modules.Fleet.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-fleet`  
> **Phase:** 1 (Catalog & Core Foundation — Parallel Stream 2)  
> **PostgreSQL Schema:** `fleet`  
> **References:** [be/docs/domain-model-entities-updated.md](../../../docs/domain-model-entities-updated.md) (Section 4.7 & 5)

---

## 1. Objectives & Scope
The Fleet module manages vehicle inventory, photo galleries, vehicle compliance documents (registration, insurance, inspection certificate), geo-location pinning, and the vehicle approval lifecycle (Draft -> PendingApproval -> Active/Paused/Rejected/Archived).

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after feat/shared-kernel is merged
git checkout -b feat/module-fleet

# Build module
dotnet build src/Modules/Fleet/CarRental.Modules.Fleet/CarRental.Modules.Fleet.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks` (Result, Guard, GeoPoint, Address, FileReference, AggregateRoot).
- **Contracts Referenced:** `CarRental.Modules.Users.Contracts` (`IUsersApi.IsVerifiedOwnerAsync`).
- **Downstream Consumers of Fleet Contracts:**
  - `Search` (`VehicleApproved`, `VehicleListingChanged`, `VehicleStatusChanged`).
  - `Availability` (`VehicleStatusChanged` with status `Archived` to release blocks).
  - `Bookings` (`IFleetApi.GetVehicleSummaryAsync`).
  - `Notifications`, `Reporting`.

---

## 4. Contracts Project (`CarRental.Modules.Fleet.Contracts`)
### 4.1 Synchronous Interface (`IFleetApi`)
```csharp
public interface IFleetApi
{
    Task<Result<VehicleSummaryDto>> GetVehicleSummaryAsync(Guid vehicleId, CancellationToken ct = default);
}
```

### 4.2 DTOs
- `VehicleSummaryDto(Guid VehicleId, Guid OwnerId, string Status, string Title, string Brand, string Model, string? PrimaryPhotoUrl, Address Address)`

### 4.3 Published Integration Events (4 Published Events)
- `VehicleApproved(Guid EventId, DateTimeOffset OccurredAt, Guid VehicleId, Guid OwnerId, Guid AdminId)`
- `VehicleRejected(Guid EventId, DateTimeOffset OccurredAt, Guid VehicleId, Guid OwnerId, string Reason)`
- `VehicleListingChanged(Guid EventId, DateTimeOffset OccurredAt, Guid VehicleId, string Title, string Brand, string Model, int Year, double Lat, double Lon, string City, string District, string? PrimaryPhotoUrl)`
- `VehicleStatusChanged(Guid EventId, DateTimeOffset OccurredAt, Guid VehicleId, string NewStatus)` (Published when vehicle is paused, resumed to active, or archived).

*(Note: `VehicleSubmitted` is an internal domain event).*

---

## 5. Domain Layer (`Entities/`, `Enums/`, `ValueObjects/`)

### 5.1 Enums & Strongly-Typed IDs
- `readonly record struct VehicleId(Guid Value)`
- `VehicleStatus` (`Draft`, `PendingApproval`, `Active`, `Paused`, `Rejected`, `Archived`)
- `TransmissionType` (`Manual`, `Automatic`)
- `FuelType` (`Petrol`, `Diesel`, `Hybrid`, `Electric`)
- `VehicleDocumentType` (`Registration`, `Insurance`, `InspectionCertificate`)
- `VehicleFeature` (Enum / Flags: `AirConditioning`, `Gps`, `Dashcam`, `Bluetooth`, `Sunroof`, `ReverseCamera`, `ChildSeat`, `SpareTire`)

### 5.2 Value Objects & Child Entities
- `LicensePlate(string Value)`: Normalized Vietnamese plate format (e.g. `51K-123.45`). Unique in schema.
- `VehicleRules(int MinRenterAge, int MinLicenseYears, bool AllowDelivery, string? FuelPolicy, string? Notes)`
- `VehiclePhoto` (Child Entity): `Guid Id`, `FileReference Photo`, `int SortOrder`, `bool IsPrimary`.
- `VehicleDocument` (Child Entity): `Guid Id`, `VehicleDocumentType Type`, `FileReference File`, `DateOnly? ExpiresOn`, `DocumentStatus Status`.

### 5.3 Aggregate Root: `Vehicle`
- State Machine Transitions:
  ```
  Draft ──Submit──► PendingApproval ──Approve──► Active ⇄ Pause/Resume ⇄ Paused
                           │                       │
                           └─Reject──► Rejected    └─Archive──► Archived (terminal)
  ```
- Core Invariants:
  - `Year`: Must be >= 1990 and <= current year.
  - `Submit()` requires:
    - Minimum 5 photos with exactly 1 primary photo.
    - Valid registration document and unexpired insurance document.
    - Valid `GeoPoint` in `Address`.
  - `LicensePlate` cannot be modified after approval.
  - Only `Active` vehicles can be rented or searched.
  - `Archived` is a terminal state.

---

## 6. Persistence & EF Core (`Data/`)
- `FleetDbContext`: `HasDefaultSchema("fleet")`.
- `IEntityTypeConfiguration<Vehicle>`:
  - `xmin` concurrency token.
  - Unique index on `LicensePlate`.
  - PostGIS index on `Address.Location`.
  - Table splitting or owned entities for `VehicleRules`, `VehiclePhoto`, `VehicleDocument`.
- Initial migration: `AddFleetInitialMigration`.

---

## 7. Application Services & Endpoints (`Services/`, `Endpoints/`)
- `FleetApiService`: Implements `IFleetApi`.
- Minimal API Endpoints (`MapGroup("/api/vehicles")`):
  - `POST /` (Draft creation by Owner)
  - `PUT /{id}` (Update details)
  - `POST /{id}/photos` / `DELETE /{id}/photos/{photoId}`
  - `POST /{id}/documents`
  - `POST /{id}/submit`
  - `POST /{id}/pause` / `POST /{id}/resume`
  - Admin endpoints (`RequireAuthorization("Admin")`):
    - `POST /admin/vehicles/{id}/approve`
    - `POST /admin/vehicles/{id}/reject`

---

## 8. Step-by-Step Task Checklist
- [ ] Implement Enums, `VehicleId`, `LicensePlate`, `VehicleRules`.
- [ ] Implement `VehiclePhoto` and `VehicleDocument` entities.
- [ ] Implement `Vehicle` aggregate root with state machine and invariant validations.
- [ ] Define `IFleetApi`, DTOs, and integration events in `CarRental.Modules.Fleet.Contracts`.
- [ ] Setup `FleetDbContext`, PostGIS mappings, and EF Core entity configurations.
- [ ] Create EF Core migration for `fleet` schema.
- [ ] Build Minimal API endpoints with authorization policies.
- [ ] Domain unit tests:
  - `Submit_WithFewerThan5Photos_ShouldFail`
  - `Submit_WithExpiredInsurance_ShouldFail`
  - `Approve_FromPendingApproval_ShouldTransitionToActive`
  - `Archive_ShouldBecomeTerminalState`
- [ ] Integration tests using Testcontainers PostgreSQL + PostGIS.
