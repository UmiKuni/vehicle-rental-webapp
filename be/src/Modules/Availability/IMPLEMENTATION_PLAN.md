# Availability Module — Implementation Plan

> **Module Directory:** `src/Modules/Availability`  
> **Projects:** `CarRental.Modules.Availability.csproj`, `CarRental.Modules.Availability.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-availability`  
> **Phase:** 1 (Catalog & Core Foundation — Parallel Stream 4)  
> **PostgreSQL Schema:** `availability`  
> **References:** [be/docs/domain-model-entities-reworked.md](../../../docs/domain-model-entities-reworked.md) (Section 4.3 & 5)

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
| **Aggregate Root** | `VehicleCalendar` with in-memory block collection | `VehicleCalendar` (table `VehicleCalendars`, PK `VehicleId`, `LastSyncedAt`). Loads only overlapping entries for target period. |
| **Child Entity** | `AvailabilityBlock` (table `availability_blocks`) with `HoldExpiresAt`, `ReleasedAt` | `CalendarEntry` (table `CalendarEntries`) with `Id`, `VehicleId`, `Type` (`Booked`/`Hold`/`OwnerBlocked`/`Maintenance`), `TimeRange` (`tstzrange`), `ExpiresAt` (null for `Booked`/blocks; <= 12h for `Hold`), `BookingId` (nullable). |
| **Double-Booking Invariant** | Application-level checks | Two-tier guard: Domain invariant check + PostgreSQL exclusion constraint (`btree_gist` on `(VehicleId WITH =, TimeRange WITH &&)`). |
| **Published Events** | 0 published events | **0 published events** (all operations are invoked synchronously by `Bookings`). |
| **Contracts** | `PlaceHoldAsync`, `ExtendHoldAsync`, `ConfirmHoldAsync`, `ReleaseHoldAsync`, `AreAvailableAsync` | `IAvailabilityApi.PlaceHoldAsync(vehicleId, bookingId, range, ttl)`, `ConfirmHoldAsync(vehicleId, bookingId)`, `ReleaseHoldAsync(vehicleId, bookingId)`, `CheckAvailabilityAsync(vehicleId, range)`. |
| **Table Naming** | snake_case tables | Retained snake_case tables (`vehicle_calendars`, `calendar_entries`) matching PostgreSQL conventions. |

---

## 1. Objectives & Scope
The Availability module is the core concurrency protector against double-booking. It manages temporary rental holds (≤ 12h TTL), finalized bookings, owner blocks, and vehicle maintenance intervals using domain invariants combined with a PostgreSQL `tstzrange` exclusion constraint (BR-05).

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
- **Shared Kernel:** `CarRental.BuildingBlocks` (DateTimeRange, Result, Guard, AggregateRoot, Entity).
- **Contracts Referenced:** `CarRental.Modules.Fleet.Contracts` (`IFleetApi.GetVehicleSummaryAsync` for owner block permission).
- **Downstream Consumers of Availability Contracts:**
  - `Bookings`: Calls `IAvailabilityApi.PlaceHoldAsync`, `ConfirmHoldAsync`, and `ReleaseHoldAsync` synchronously.

---

## 4. Contracts Project (`CarRental.Modules.Availability.Contracts`)

### 4.1 Synchronous Interface (`IAvailabilityApi`)
```csharp
public interface IAvailabilityApi
{
    Task<Result> PlaceHoldAsync(
        Guid vehicleId, 
        Guid bookingId, 
        DateTimeRange range, 
        TimeSpan ttl, 
        CancellationToken ct = default);

    Task<Result> ConfirmHoldAsync(
        Guid vehicleId, 
        Guid bookingId, 
        CancellationToken ct = default);

    Task<Result> ReleaseHoldAsync(
        Guid vehicleId, 
        Guid bookingId, 
        CancellationToken ct = default);

    Task<bool> CheckAvailabilityAsync(
        Guid vehicleId, 
        DateTimeRange range, 
        CancellationToken ct = default);
}
```

### 4.2 Published Integration Events
- **0 Published Events** (Per Section 5 of `domain-model-entities-reworked.md`, all Availability operations are synchronous and consume 0 events).

---

## 5. Domain Layer (`Entities/`, `Enums/`)

### 5.1 Enums & Strongly-Typed IDs
- `CalendarEntryType`: `Booked`, `Hold`, `OwnerBlocked`, `Maintenance`.

### 5.2 Child Entity: `CalendarEntry`
- Table `CalendarEntries`:
  - `Guid Id`
  - `Guid VehicleId` (indexed)
  - `CalendarEntryType Type` (`Booked`/`Hold`/`OwnerBlocked`/`Maintenance`)
  - `DateTimeRange TimeRange` (maps to PostgreSQL `tstzrange`)
  - `DateTimeOffset? ExpiresAt` (null for `Booked`/blocks; <= 12h for `Hold`, BR-07)
  - `Guid? BookingId` (nullable, indexed)
- Rules:
  - `IsActive(DateTimeOffset now)`: `Type != CalendarEntryType.Hold || (ExpiresAt.HasValue && ExpiresAt.Value > now)`.

### 5.3 Aggregate Root: `VehicleCalendar`
- Table `VehicleCalendars`:
  - `Guid VehicleId` (Aggregate Root Primary Key)
  - `DateTimeOffset LastSyncedAt`
  - Child collection: `IReadOnlyCollection<CalendarEntry> Entries`.
- Methods & Invariants:
  ```csharp
  Result PlaceHold(Guid bookingId, DateTimeRange range, TimeSpan ttl, DateTimeOffset now); // ttl <= 12h
  Result ConfirmHold(Guid bookingId, DateTimeOffset now); // Hold -> Booked, ExpiresAt = null
  Result Release(Guid bookingId, DateTimeOffset now); // removes the booking's entries
  Result Block(CalendarEntryType type /* OwnerBlocked | Maintenance */, DateTimeRange range, DateTimeOffset now);
  Result Unblock(Guid entryId, DateTimeOffset now);
  int ExpireHolds(DateTimeOffset now); // removes expired holds
  bool IsAvailable(DateTimeRange range, DateTimeOffset now);
  ```
- Invariants:
  - Active entries never overlap (domain check AND Postgres exclusion constraint, BR-05).
  - `Hold` always has `ExpiresAt` within 12h.
  - `Booked`/`OwnerBlocked`/`Maintenance` have `ExpiresAt = null`.
  - Expired `Hold` entries are treated as inactive and can be pruned.
  - Only `Hold` entries can be confirmed.
  - Owner blocks cannot overlap `Booked` or active `Hold` entries.

---

## 6. Persistence & EF Core (`Data/`)
- `AvailabilityDbContext`: `HasDefaultSchema("availability")`.
- Table mapping (snake_case):
  - `vehicle_calendars` (PK `vehicle_id`)
  - `calendar_entries` (FK `vehicle_id`)
- PostgreSQL `btree_gist` exclusion constraint on `CalendarEntries`:
  ```sql
  ALTER TABLE availability."CalendarEntries"
  ADD CONSTRAINT no_overlapping_active_entries
  EXCLUDE USING gist (
      "VehicleId" WITH =,
      tstzrange("TimeRange_Start", "TimeRange_End") WITH &&
  );
  ```

---

## 7. Application Services & Endpoints (`Services/`, `Endpoints/`)
- `AvailabilityApiService`: Implements `IAvailabilityApi`.
- Background Job: `ExpireHoldsJob` (runs periodically as safety-net pruning; Booking owns its rental deadlines).
- Minimal API Endpoints (`MapGroup("/api/availability")`):
  - `GET /vehicles/{id}/check` (`startTime`, `endTime`)
  - `POST /vehicles/{id}/blocks` (owner blocks dates; verifies ownership via `IFleetApi`)

---

## 8. Step-by-Step Task Checklist
- [x] Document updated architecture and database entity mapping.
- [ ] Implement `VehicleCalendar` aggregate root and `CalendarEntry` child entity.
- [ ] Implement `IAvailabilityApi` in `CarRental.Modules.Availability.Contracts`.
- [ ] Setup `AvailabilityDbContext` with schema `availability` and `tstzrange` mapping.
- [ ] Add domain unit tests for `PlaceHold`, `ConfirmHold`, `Release`, `ExpireHolds`, and overlap rejection.
- [ ] Verify zero EF/ASP.NET dependencies in `Entities/` via `DomainEncapsulationTests`.
