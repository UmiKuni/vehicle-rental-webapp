# Handover Module — Implementation Plan

> **Module Directory:** `src/Modules/Handover`  
> **Projects:** `CarRental.Modules.Handover.csproj`, `CarRental.Modules.Handover.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-handover`  
> **Phase:** 3 (Physical Fulfillment & Trust — Parallel Stream 1)  
> **PostgreSQL Schema:** `handover`  
> **References:** [be/docs/domain-model-entities-reworked.md](../../../docs/domain-model-entities-reworked.md) (Section 4.5 & 5)

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
| **Aggregate Root** | Single `Handover` aggregate covering both pickup and return with nested `Inspection` entities | `Handover` (table `Handovers`) with `Id`, `BookingId`, `Type` (`Pickup`/`Return`), `ServerReceivedAt`, `Status` (`Draft`/`Confirmed`), `ConfirmedByCustomerAt`, `ConfirmedByOwnerAt`. One row per `(BookingId, Type)`. |
| **Child Entities** | `InspectionPhoto`, `DamageItem`, `Inspection` | `HandoverPhoto` (table `HandoverPhotos`, `Corner`: `FrontLeft`/`FrontRight`/`RearLeft`/`RearRight`/`Dash`, `StorageKey`, `FileHash`, `ClientCapturedAt`), `VehicleCondition` (table `VehicleConditions`, 1:1, `Odometer`, `FuelLevel`, `DamageNotes`). |
| **Tamper Resistance** | Storage key only | Mandates `FileHash` and server timestamp `ServerReceivedAt` (server clock, BR-13) alongside offline client timestamp `ClientCapturedAt` (BR-14). |
| **Local Read Model** | None | `HandoverContext` (`BookingId`, `VehicleId`, `CustomerId`, `OwnerId`, `StartTime`, `EndTime`) populated asynchronously from `BookingPaid`. |
| **Published Events** | 2 events (`PickupCompleted`, `ReturnCompleted`) | **1 published event**: `HandoverConfirmed` (carries `BookingId`, `Type`, `Odo`, `FuelLevel`, `ConfirmedAt`; for `Return` also `DistanceKm`). |
| **Table Naming** | snake_case tables | Retained snake_case tables (`handovers`, `handover_photos`, `vehicle_conditions`, `handover_contexts`) matching PostgreSQL conventions. |

---

## 1. Objectives & Scope
The Handover module manages vehicle custody transfer during pickup and return. It enforces photographic evidence across 4 corners and the dashboard with cryptographic file hashes (BR-13), logs odometer and fuel level, and ensures immutability once both customer and owner confirm the record.

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after Phase 2 contracts are available
git checkout -b feat/module-handover

# Build module
dotnet build src/Modules/Handover/CarRental.Modules.Handover/CarRental.Modules.Handover.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks` (Result, Guard, AggregateRoot, Entity).
- **Consumes Events from:**
  - `Bookings`: `BookingPaid` -> Creates local `HandoverContext`; `BookingCancelled` -> Closes context.
- **Downstream Consumers of Handover Contracts:**
  - `Bookings`: Consumes `HandoverConfirmed` -> `StartTrip` (Pickup) or `CompleteTrip` (Return).

---

## 4. Contracts Project (`CarRental.Modules.Handover.Contracts`)

### 4.1 Synchronous Interface
*(No synchronous interface required; interactions are event-driven via Wolverine Outbox).*

### 4.2 Published Integration Events (1 Published Event)
- `HandoverConfirmed(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, string Type, int Odometer, int FuelLevel, DateTimeOffset ConfirmedAt, int? DistanceKm)`

---

## 5. Domain Layer (`Entities/`, `Enums/`)

### 5.1 Enums & Strongly-Typed IDs
- `readonly record struct HandoverId(Guid Value)`
- `HandoverType`: `Pickup`, `Return`.
- `HandoverStatus`: `Draft`, `Confirmed`.
- `PhotoCorner`: `FrontLeft`, `FrontRight`, `RearLeft`, `RearRight`, `Dash`.

### 5.2 Child Entities & Read Model
- **`HandoverPhoto`** (Table `HandoverPhotos`):
  - Properties: `Guid Id`, `Guid HandoverId`, `PhotoCorner Corner`, `string StorageKey`, `string FileHash` (tamper verification, BR-13), `DateTimeOffset ClientCapturedAt` (BR-14).
- **`VehicleCondition`** (Table `VehicleConditions`, 1:1, PK `HandoverId`):
  - Properties: `Guid HandoverId`, `int Odometer`, `int FuelLevel` (0..100), `string? DamageNotes`.
- **`HandoverContext`** (Table `HandoverContexts`, Local Read Model):
  - Properties: `Guid BookingId` (PK), `Guid VehicleId`, `Guid CustomerId`, `Guid OwnerId`, `DateTimeOffset StartTime`, `DateTimeOffset EndTime`, `bool IsActive`.

### 5.3 Aggregate Root: `Handover`
- Table `Handovers`:
  - `Guid Id`
  - `Guid BookingId` (indexed)
  - `HandoverType Type` (`Pickup`/`Return`)
  - `DateTimeOffset ServerReceivedAt` (server clock timestamp, BR-13)
  - `HandoverStatus Status` (`Draft`/`Confirmed`)
  - `DateTimeOffset? ConfirmedByCustomerAt`
  - `DateTimeOffset? ConfirmedByOwnerAt`
  - Unique index on `(BookingId, Type)`
  - Child navigation: `IReadOnlyCollection<HandoverPhoto> Photos`, `VehicleCondition? Condition`.
- Domain Methods & Invariants:
  ```csharp
  static Result<Handover> Start(HandoverContext ctx, HandoverType type, Guid startedBy, DateTimeOffset serverNow);
  Result AddPhoto(PhotoCorner corner, string storageKey, string fileHash, DateTimeOffset clientCapturedAt); // Draft only
  Result SetCondition(int odo, int fuelLevel, string? damageNote); // Draft only
  Result Confirm(Guid actorId, DateTimeOffset now); // Each party confirms once; both confirmed -> Confirmed + immutable, raises HandoverConfirmed
  ```
- Invariants:
  - Only the booking's verified customer and owner can act.
  - Data is mutable only in `Draft` and **strictly immutable after `Confirmed`**.
  - Confirmation requires 5 mandatory photos (4 corners + dash) and valid `VehicleCondition`.
  - Fuel level must be 0..100.
  - Mandatory `FileHash` for all uploaded photos.
  - `Return` can only start after `Pickup` is `Confirmed`, and return `Odometer` must be >= pickup `Odometer`.

---

## 6. Persistence & EF Core (`Data/`)
- `HandoverDbContext`: `HasDefaultSchema("handover")`.
- Table mapping (snake_case):
  - `handovers` (Aggregate Root with `xmin` row version)
  - `handover_photos` (FK `handover_id`)
  - `vehicle_conditions` (PK/FK `handover_id`)
  - `handover_contexts` (PK `booking_id`)
- Unique index on `(BookingId, Type)`.

---

## 7. Event Handlers & Endpoints (`EventHandlers/`, `Endpoints/`)
- `BookingPaidHandler`: Inserts `HandoverContext`.
- `BookingCancelledHandler`: Deactivates `HandoverContext`.
- Minimal API Endpoints (`MapGroup("/api/handovers")`):
  - `POST /` (start Pickup or Return)
  - `POST /{id}/photos` (upload corner photo with hash)
  - `PUT /{id}/condition` (set odometer and fuel)
  - `PUT /{id}/confirm` (customer or owner confirmation sign-off)

---

## 8. Step-by-Step Task Checklist
- [x] Document updated architecture and database entity mapping.
- [ ] Implement `Handover` aggregate root, `HandoverPhoto`, `VehicleCondition`, and `HandoverContext`.
- [ ] Implement `HandoverConfirmed` event in `CarRental.Modules.Handover.Contracts`.
- [ ] Setup `HandoverDbContext` with schema `handover`.
- [ ] Add domain unit tests for photo requirements, hash validation, dual sign-off immutability, and odometer progression.
- [ ] Verify zero EF/ASP.NET dependencies in `Entities/` via `DomainEncapsulationTests`.
