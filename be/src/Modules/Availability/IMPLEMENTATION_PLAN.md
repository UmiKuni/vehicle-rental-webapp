# Availability Module — Implementation Plan

> **Module Directory:** `src/Modules/Availability`  
> **Projects:** `CarRental.Modules.Availability.csproj`, `CarRental.Modules.Availability.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-availability`  
> **Phase:** 1 (Catalog & Core Foundation — Parallel Stream 4)  
> **PostgreSQL Schema:** `availability`  
> **References:** [be/docs/domain-model-entities-updated.md](../../../docs/domain-model-entities-updated.md) (Section 4.3 & 5)

---

## 1. Objectives & Scope
The Availability module is the core concurrency protector against double-booking. It manages temporary rental holds (12h TTL), finalized bookings, owner blocks, and vehicle maintenance intervals using domain invariants combined with a PostgreSQL `tstzrange` exclusion constraint.

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after feat/shared-kernel is merged
git checkout -b feat/module-availability

# Build module
dotnet build src/Modules/Availability/CarRental.Modules.Availability/CarRental.Modules.Availability.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks` (DateTimeRange, Result, Guard, AggregateRoot).
- **Consumes Events from:**
  - `Bookings`: `BookingConfirmed` -> Confirm hold; `BookingRejected` / `BookingCancelled` / `BookingExpired` -> Release blocks (idempotent compensation).
  - `Fleet`: `VehicleStatusChanged` (status `Archived`) -> Release all future blocks.
- **Downstream Consumers of Availability Contracts:**
  - `Bookings`: Calls `IAvailabilityApi.PlaceHoldAsync`, `ExtendHoldAsync`, and `ReleaseHoldAsync`.
  - `Search`: Calls `IAvailabilityApi.AreAvailableAsync` (bulk check for search candidates).

---

## 4. Contracts Project (`CarRental.Modules.Availability.Contracts`)
### 4.1 Synchronous Interface (`IAvailabilityApi`)
```csharp
public interface IAvailabilityApi
{
    Task<Result<Guid>> PlaceHoldAsync(
        Guid vehicleId, 
        Guid bookingId, 
        DateTimeRange range, 
        TimeSpan ttl, 
        CancellationToken ct = default);

    Task<Result> ExtendHoldAsync(
        Guid holdId, 
        DateTimeOffset newExpiresAt, 
        CancellationToken ct = default);

    Task<Result> ConfirmHoldAsync(Guid holdId, CancellationToken ct = default);

    Task<Result> ReleaseHoldAsync(Guid bookingId, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, bool>> AreAvailableAsync(
        IReadOnlyList<Guid> vehicleIds, 
        DateTimeRange range, 
        CancellationToken ct = default);
}
```

### 4.2 Published Integration Events (0 Published Events)
Per the system event policy (small contract surface), Availability publishes **0 integration events**. All lifecycle occurrences (`HoldPlaced`, `HoldExpired`, `HoldReleased`, `SlotBooked`, `VehicleBlocked`) remain **internal domain events** handled within the module. Booking owns and enforces its own rental deadlines.

---

## 5. Domain Layer (`Entities/`, `Enums/`)

### 5.1 Enums & Strongly-Typed IDs
- `BlockType` (`Hold`, `Booked`, `OwnerBlocked`, `Maintenance`)

### 5.2 Child Entity: `AvailabilityBlock`
- Properties:
  - `Guid Id`
  - `DateTimeRange Range`
  - `BlockType Type`
  - `Guid? BookingId`
  - `DateTimeOffset? HoldExpiresAt` (mandatory when `Type == Hold`)
  - `string? Reason`
  - `DateTimeOffset CreatedAt`
  - `DateTimeOffset? ReleasedAt`
- Rules:
  - `IsActive(DateTimeOffset now)`: `ReleasedAt == null && (Type != BlockType.Hold || HoldExpiresAt > now)`.

### 5.3 Aggregate Root: `VehicleCalendar`
- Properties:
  - `Guid VehicleId` (Aggregate Root Key)
  - `List<AvailabilityBlock> _blocks`
- Methods & Invariants:
  - `PlaceHold(Guid bookingId, DateTimeRange range, TimeSpan ttl, DateTimeOffset now) : Result<Guid>`:
    - Hold TTL capped at 12 hours from creation.
    - Adds 2-hour buffer before and after rental period.
    - Rejects overlapping active blocks.
  - `ExtendHold(Guid holdId, DateTimeOffset newExpiresAt, DateTimeOffset now) : Result`:
    - Executed once when owner approves booking, up to booking's `PaymentDeadline`.
  - `ConfirmHold(Guid holdId, DateTimeOffset now) : Result`:
    - Converts `Hold` -> `Booked`, clears `HoldExpiresAt`.
  - `Release(Guid bookingId, DateTimeOffset now) : Result`:
    - Sets `ReleasedAt = now` on all active blocks for the booking.
  - `BlockByOwner(DateTimeRange range, string reason, DateTimeOffset now) : Result<Guid>`
  - `Unblock(Guid blockId, DateTimeOffset now) : Result`
  - `ExpireHolds(DateTimeOffset now) : int`: Marks expired holds as released (safety-net cleanup).
  - `IsAvailable(DateTimeRange range, DateTimeOffset now) : bool`

---

## 6. Persistence & EF Core (`Data/`)
- `AvailabilityDbContext`: `HasDefaultSchema("availability")`.
- Database Constraints (PostgreSQL `btree_gist`):
  - Add raw SQL migration:
    ```sql
    CREATE EXTENSION IF NOT EXISTS btree_gist;
    
    ALTER TABLE availability.availability_blocks 
    ADD CONSTRAINT no_overlapping_blocks 
    EXCLUDE USING gist (
        vehicle_id WITH =,
        tstzrange(range_start, range_end) WITH &&
    ) WHERE (released_at IS NULL);
    ```
- Migration: `AddAvailabilityInitialMigration`.

---

## 7. Background Jobs & Handlers (`Jobs/`, `EventHandlers/`)
- `ExpireHoldsJob`: Runs every minute (safety-net cleanup only).
- Wolverine Event Handlers:
  - `BookingConfirmedHandler` -> invokes `ConfirmHoldAsync`.
  - `BookingRejectedHandler` / `BookingCancelledHandler` / `BookingExpiredHandler` -> invokes `ReleaseHoldAsync`.
  - `VehicleStatusChangedHandler` -> when new status is `Archived`, releases all future blocks.

---

## 8. Step-by-Step Task Checklist
- [ ] Implement `BlockType` enum and `AvailabilityBlock` entity.
- [ ] Implement `VehicleCalendar` aggregate root with buffer calculations, overlap detection, and `ExtendHold`.
- [ ] Implement `IAvailabilityApi` in `CarRental.Modules.Availability.Contracts`.
- [ ] Configure `AvailabilityDbContext` and EF mappings.
- [ ] Write PostgreSQL migration including `btree_gist` exclusion constraint.
- [ ] Implement background worker `ExpireHoldsJob`.
- [ ] Wire Wolverine handlers for booking and vehicle status integration events.
- [ ] Unit tests:
  - `PlaceHold_OverlappingWithExistingHold_ShouldFail`
  - `PlaceHold_WithOver12HoursTtl_ShouldBeClampedOrFailed`
  - `ExtendHold_AfterOwnerApproval_ExtendsHoldExpiration`
  - `ConfirmHold_WhenExpired_ShouldFail`
  - `Release_ShouldReleaseAllBlocksForBooking`
- [ ] Concurrency test with Testcontainers:
  - Spin up 20 concurrent threads trying to hold the same car for overlapping dates; verify exactly 1 succeeds and DB constraint rejects the others.

- [ ] Concurrency test with Testcontainers:
  - Spin up 20 concurrent threads trying to hold the same car for overlapping dates; verify exactly 1 succeeds and DB constraint rejects the others.
