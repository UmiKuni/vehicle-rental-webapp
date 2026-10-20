# Fleet Module — Implementation Plan

> **Module Directory:** `src/Modules/Fleet`  
> **Projects:** `CarRental.Modules.Fleet.csproj`, `CarRental.Modules.Fleet.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-fleet`  
> **Phase:** 1 (Catalog & Core Foundation — Parallel Stream 2)  
> **PostgreSQL Schema:** `fleet`  
> **References:** [be/docs/domain-model-entities-reworked.md](../../../docs/domain-model-entities-reworked.md) (Section 4.7 & 5)

> [!IMPORTANT]
> **Implementation Scope Boundary:**
> Current implementation scope is strictly restricted to **Contracts, Domain Entities, and EF Core mappings (Data/)**.
> - **In Scope:** Aggregate roots, child entities, value objects, domain events, strongly-typed IDs, Contracts (interfaces, DTOs, enums, integration events), DbContext, and IEntityTypeConfiguration<T>.
> - **Out of Scope (Do NOT build yet):** Application services, repositories, minimal API endpoints, controllers, and background jobs. Stop proceeding immediately after domain entities and EF Core data configurations are completed.
> - **EF Core Migrations Policy:** Do NOT run migrations during entity construction. Database migrations (dotnet ef migrations add) will only be generated after all entities across all modules have been fully implemented and verified.


---

## 0. Notification of Changes (Old Design vs. Reworked Source-of-Truth)

| Dimension | Previous (Old Design) | Reworked (Source of Truth from `Thiết kế hệ thống, CSDL và API`) |
|---|---|---|
| **Aggregate Root** | `Vehicle` with `Year`, `Make`/`Model`, `Seats`, `Status` (`Draft`/`PendingApproval`/`Active`/`Paused`/`Rejected`/`Archived`), `VehicleRules` VO, `LicensePlate` VO | `Vehicle` (table `Vehicles`) with `Id`, `OwnerId`, `PlateNumber` (unique), `BrandId`, `ModelId`, `Transmission` (`Auto`/`Manual`), `Seats`, `Status` (`Draft`/`PendingApproval`/`Published`/`Hidden`), `Location` (`GeoPoint`), `Address`, `HiddenByAdmin`. |
| **Child Entities** | `VehiclePhoto` (with `FileReference`, `SortOrder`), `VehicleDocument` (registration, insurance, inspection certificates) | `VehicleImage` (table `VehicleImages`, `StorageKey`, `IsPrimary`), `VehicleApproval` (table `VehicleApprovals`, `AdminId`, `Decision`: `Approved`/`Rejected`, `Reason`, `CreatedAt`). |
| **Reference Catalogs** | None | `VehicleBrand` (`Id`, `Name`), `VehicleModel` (`Id`, `BrandId`, `Name`) [ASSUMPTION]. |
| **State Machine** | `Draft` -> `PendingApproval` -> `Active` <-> `Paused`, `Archived` | `Draft` -> `PendingApproval` -> `Published` -> `Hidden`. Admin reject returns to `Draft`. Admin unpublish sets `HiddenByAdmin = true`. |
| **Published Events** | 4 events (`VehicleApproved`, `VehicleRejected`, `VehicleListingChanged`, `VehicleStatusChanged`) | **2 published events**: `VehiclePublished` (full snapshot for Search/Notifications), `VehicleUnpublished`. |
| **Contracts** | `IFleetApi.GetVehicleSummaryAsync` (extended DTO) | `IFleetApi.GetVehicleSummaryAsync(vehicleId)` returning `VehicleSummaryDto` (`OwnerId`, `Status`, `Brand`, `Model`, `Seats`, `Transmission`, `PlateNumber`, `PrimaryImageKey`, `Location`). |
| **Table Naming** | snake_case tables | Retained snake_case tables (`vehicles`, `vehicle_images`, `vehicle_approvals`, `vehicle_brands`, `vehicle_models`) matching PostgreSQL conventions. |

---

## 1. Objectives & Scope
The Fleet module manages vehicle inventory, image galleries, vehicle brands/models catalog, geo-location mapping, and the vehicle approval/publishing lifecycle. It enforces invariants such as uniqueness of license plates, required primary images, and owner verification.

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
- **Shared Kernel:** `CarRental.BuildingBlocks` (Result, Guard, GeoPoint, AggregateRoot, Entity).
- **Downstream Consumers of Fleet Contracts:**
  - `Search`: Consumes `VehiclePublished` and `VehicleUnpublished` to update search index.
  - `Bookings`: Calls `IFleetApi.GetVehicleSummaryAsync` to check ownership and `Published` status.
  - `Pricing`, `Availability`: Call `IFleetApi.GetVehicleSummaryAsync` to verify vehicle ownership.
  - `Notifications`: Consumes `VehiclePublished` and `VehicleUnpublished`.

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
- `VehicleSummaryDto(Guid VehicleId, Guid OwnerId, string Status, Guid BrandId, string BrandName, Guid ModelId, string ModelName, string PlateNumber, string Transmission, int Seats, string? PrimaryImageKey, GeoPoint Location, string Address)`

### 4.3 Published Integration Events (2 Published Events)
- `VehiclePublished(Guid EventId, DateTimeOffset OccurredAt, Guid VehicleId, Guid OwnerId, string Brand, string Model, int Seats, string Transmission, GeoPoint Location, string Address)`
- `VehicleUnpublished(Guid EventId, DateTimeOffset OccurredAt, Guid VehicleId, Guid OwnerId, string Reason)`

---

## 5. Domain Layer (`Entities/`, `Enums/`)

### 5.1 Enums & Strongly-Typed IDs
- `readonly record struct VehicleId(Guid Value)`
- `VehicleStatus`: `Draft`, `PendingApproval`, `Published`, `Hidden`.
- `TransmissionType`: `Auto`, `Manual`.
- `ApprovalDecision`: `Approved`, `Rejected`.

### 5.2 Child Entities & Catalogs
- **`VehicleImage`** (Table `VehicleImages`):
  - Properties: `Guid Id`, `Guid VehicleId`, `string StorageKey`, `bool IsPrimary`.
- **`VehicleApproval`** (Table `VehicleApprovals`):
  - Properties: `Guid Id`, `Guid VehicleId`, `Guid AdminId`, `ApprovalDecision Decision`, `string? Reason`, `DateTimeOffset CreatedAt`.
- **`VehicleBrand`** (Table `VehicleBrands` [ASSUMPTION]):
  - Properties: `Guid Id`, `string Name`.
- **`VehicleModel`** (Table `VehicleModels` [ASSUMPTION]):
  - Properties: `Guid Id`, `Guid BrandId`, `string Name`.

### 5.3 Aggregate Root: `Vehicle`
- Table `Vehicles`:
  - `Guid Id`
  - `Guid OwnerId` (indexed)
  - `string PlateNumber` (unique, indexed)
  - `Guid BrandId`
  - `Guid ModelId`
  - `TransmissionType Transmission` (`Auto`/`Manual`)
  - `int Seats`
  - `VehicleStatus Status` (`Draft`/`PendingApproval`/`Published`/`Hidden`)
  - `GeoPoint Location` (SRID 4326 PostGIS geometry)
  - `string Address`
  - `bool HiddenByAdmin`
  - Child collections: `IReadOnlyCollection<VehicleImage> Images`, `IReadOnlyCollection<VehicleApproval> Approvals`.
- State Machine Transitions:
  ```
  Draft ──SubmitForApproval──► PendingApproval ──Approve(Admin)──► Published ──Hide(Owner)──► Hidden
                                      │                                │                          ▲
                                      └─Reject(Admin)──► Draft         └─Unpublish(Admin)─────────┘
                                                                           (HiddenByAdmin = true)
  ```
- Domain Invariants & Methods:
  ```csharp
  static Result<Vehicle> Create(Guid ownerId, string plateNumber, Guid brandId, Guid modelId, TransmissionType transmission, int seats, GeoPoint location, string address);
  Result UpdateDetails(Guid brandId, Guid modelId, TransmissionType transmission, int seats, GeoPoint location, string address);
  Result AddImage(string storageKey);
  Result RemoveImage(Guid imageId);
  Result SetPrimaryImage(Guid imageId);
  Result SubmitForApproval(); // Requires primary image and >= 3 images [ASSUMPTION], valid location
  Result Approve(Guid adminId); // PendingApproval -> Published, raises VehiclePublished
  Result Reject(Guid adminId, string reason); // PendingApproval -> Draft
  Result Unpublish(Guid adminId, string reason); // Published -> Hidden (HiddenByAdmin = true), raises VehicleUnpublished
  Result Hide(); // Published -> Hidden (by owner)
  Result Show(); // Hidden -> Published (only if !HiddenByAdmin)
  ```

---

## 6. Persistence & EF Core (`Data/`)
- `FleetDbContext`: `HasDefaultSchema("fleet")`.
- Table mapping (snake_case):
  - `vehicles` (Aggregate Root with `xmin` row version)
  - `vehicle_images` (FK `vehicle_id`)
  - `vehicle_approvals` (FK `vehicle_id`)
  - `vehicle_brands`, `vehicle_models`
- Spatial PostGIS column for `Location` with GIST index.
- Unique index on `PlateNumber`.

---

## 7. Application Services & Endpoints (`Services/`, `Endpoints/`)
- `FleetApiService`: Implements `IFleetApi`.
- Minimal API Endpoints (`MapGroup("/api/fleet/vehicles")`):
  - `POST /` (creates Draft and submits for approval)
  - `PUT /{id}` (update details)
  - `GET /{id}` (details)
  - `PUT /{id}/approve` (Admin: approve or unpublish)

---

## 8. Step-by-Step Task Checklist
- [x] Document updated architecture and database entity mapping.
- [ ] Implement `Vehicle` aggregate root, `VehicleImage`, `VehicleApproval`, and brand/model catalogs.
- [ ] Implement `IFleetApi` and DTOs/events in `CarRental.Modules.Fleet.Contracts`.
- [ ] Setup `FleetDbContext` with schema `fleet` and PostGIS geometry mapping.
- [ ] Add domain unit tests for `Vehicle` invariants, state transitions, image constraints, and admin unpublish.
- [ ] Verify zero EF/ASP.NET dependencies in `Entities/` via `DomainEncapsulationTests`.
