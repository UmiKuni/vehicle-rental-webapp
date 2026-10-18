# Handover Module — Implementation Plan

> **Module Directory:** `src/Modules/Handover`  
> **Projects:** `CarRental.Modules.Handover.csproj`, `CarRental.Modules.Handover.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-handover`  
> **Phase:** 3 (Physical Fulfillment & Trust — Parallel Stream 1)  
> **PostgreSQL Schema:** `handover`  
> **References:** [be/docs/domain-model-entities-updated.md](../../../docs/domain-model-entities-updated.md) (Section 4.5 & 5)

---

## 1. Objectives & Scope
The Handover module manages the physical vehicle exchange at check-in (pickup) and check-out (return). It guarantees trustworthy documentation of odometer mileage, fuel levels, pre-existing vs. new damages, 6-point photos, and dual confirmation by both owner and renter.

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
- **Shared Kernel:** `CarRental.BuildingBlocks` (DateTimeRange, FileReference, Result, Guard, AggregateRoot).
- **Consumes Events from:**
  - `Bookings`: `BookingConfirmed` (initializes scheduled Handover), `BookingCancelled` (closes Handover).
- **Downstream Consumers of Handover Contracts:**
  - `Bookings`: Consumes `PickupCompleted` -> `StartTrip`; `ReturnCompleted` -> `CompleteTrip`.
  - `Support`: Consumes `ReturnCompleted` (carries `TripSummary.NewDamages[]`) to identify and arbitrate dispute claims.
  - `Notifications`.

---

## 4. Contracts Project (`CarRental.Modules.Handover.Contracts`)
### 4.1 Synchronous Interface (`IHandoverApi`)
```csharp
public interface IHandoverApi
{
    Task<Result<HandoverSummaryDto>> GetHandoverSummaryAsync(Guid bookingId, CancellationToken ct = default);
}
```

### 4.2 Published Integration Events (2 Published Events)
- `PickupCompleted(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, Guid VehicleId, int OdometerKm, int FuelPercent)`
- `ReturnCompleted(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, Guid VehicleId, TripSummaryDto Summary)`

*(Note: `DamageReported` is an internal domain event).*

---

## 5. Domain Layer (`Entities/`, `Enums/`, `ValueObjects/`)

### 5.1 Enums & Value Objects
- `HandoverStatus` (`Scheduled`, `PickupRecorded`, `PickupCompleted`, `ReturnRecorded`, `ReturnCompleted`)
- `InspectionType` (`Pickup`, `Return`)
- `InspectionArea` (`Front`, `Rear`, `Left`, `Right`, `Interior`, `Dashboard`)
- `DamageSeverity` (`Minor`, `Moderate`, `Severe`)
- `TripSummary(int DistanceKm, int FuelDeltaPercent, int LateMinutes, IReadOnlyList<DamageItem> NewDamages)`

### 5.2 Child Entities: `Inspection`, `DamageItem`, `InspectionPhoto`
- `InspectionPhoto`: `FileReference File`, `InspectionArea Area`.
- `DamageItem`: `InspectionArea Area`, `string Description`, `DamageSeverity Severity`, `IReadOnlyList<FileReference> Photos`.
- `Inspection`:
  - `InspectionType Type`, `Guid RecordedBy`, `DateTimeOffset RecordedAt`
  - `int OdometerKm`, `int FuelLevelPercent` (0..100), `string? Notes`
  - `List<InspectionPhoto> Photos`, `List<DamageItem> Damages`
  - `DateTimeOffset? OwnerConfirmedAt`, `DateTimeOffset? RenterConfirmedAt`
  - `bool IsFullyConfirmed => OwnerConfirmedAt != null && RenterConfirmedAt != null;`

### 5.3 Aggregate Root: `Handover`
- State Machine:
  ```
  Scheduled ──RecordPickup──► PickupRecorded ──ConfirmPickup (both)──► PickupCompleted
                                                                            │
  ReturnCompleted ◄──ConfirmReturn (both)── ReturnRecorded ◄──RecordReturn──┘
  ```
- Invariants & Business Logic:
  - `RecordPickup(Inspection)` requires at least 6 photos covering all designated areas (Front, Rear, Left, Right, Interior, Dashboard).
  - Status advances to `PickupCompleted` / `ReturnCompleted` ONLY when both Owner and Renter have confirmed.
  - Return inspection cannot be started until pickup is completed.
  - Invariant: `Return.OdometerKm >= Pickup.OdometerKm`.
  - Automatically calculates `TripSummary`:
    - `DistanceKm = Return.OdometerKm - Pickup.OdometerKm`
    - `LateMinutes = max(0, ReturnAt - ScheduledPeriod.End)`

---

## 6. Persistence & EF Core (`Data/`)
- `HandoverDbContext`: `HasDefaultSchema("handover")`.
- `IEntityTypeConfiguration<Handover>`:
  - Unique index on `BookingId`.
  - Owned entities for `ScheduledPeriod` and `TripSummary`.
  - Navigation configurations for `PickupInspection` and `ReturnInspection`.
- Migration: `AddHandoverInitialMigration`.

---

## 7. Event Handlers & Endpoints (`EventHandlers/`, `Endpoints/`)
- `BookingConfirmedHandler`: Automatically creates a new `Handover` record in `Scheduled` status.
- Minimal API Endpoints (`MapGroup("/api/handovers")`):
  - `GET /{bookingId}`
  - `POST /{bookingId}/pickup` (Submit pickup photos and odometer)
  - `POST /{bookingId}/pickup/confirm` (Owner or Renter sign-off)
  - `POST /{bookingId}/return` (Submit return inspection)
  - `POST /{bookingId}/return/confirm` (Owner or Renter sign-off)
  - `POST /{bookingId}/damages` (Explicit damage report)

---

## 8. Step-by-Step Task Checklist
- [ ] Implement Enums, `InspectionPhoto`, `DamageItem`, `TripSummary`.
- [ ] Implement `Inspection` child entity and dual-confirmation rules.
- [ ] Implement `Handover` aggregate root with 6-photo validation and trip summary calculation.
- [ ] Implement `IHandoverApi` and integration events in `CarRental.Modules.Handover.Contracts`.
- [ ] Configure `HandoverDbContext` and EF Core mappings.
- [ ] Create EF Core migration for `handover` schema.
- [ ] Implement Wolverine handler for `BookingConfirmed`.
- [ ] Build Minimal API endpoints.
- [ ] Unit tests:
  - `RecordPickup_MissingAnyOf6RequiredPhotos_ShouldFail`
  - `ConfirmPickup_SinglePartyConfirmation_ShouldNotAdvanceToCompleted`
  - `RecordReturn_WithLowerOdometerThanPickup_ShouldFail`
  - `ConfirmReturn_CalculatesTripSummaryCorrectly`
- [ ] Testcontainers integration tests.
