# Bookings Module — Implementation Plan

> **Module Directory:** `src/Modules/Bookings`  
> **Projects:** `CarRental.Modules.Bookings.csproj`, `CarRental.Modules.Bookings.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-bookings`  
> **Phase:** 2 (Rental Flow Core — Parallel Stream 1)  
> **PostgreSQL Schema:** `booking`  
> **References:** [be/docs/domain-model-entities-reworked.md](../../../docs/domain-model-entities-reworked.md) (Section 4.1, 5, 6)

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
| **Aggregate Root** | `Booking` with `Requested`, `AwaitingPayment`, `Confirmed`, `InProgress`, `Completed` | `Booking` (table `Bookings`) with `Id`, `CustomerId`, `OwnerId`, `VehicleId`, `Status` (`Pending`, `Confirmed`, `Paid`, `InProgress`, `Completed`, `Cancelled`, `Rejected`, `Expired`), `StartTime`, `EndTime`, `PickupLocation`, `PaymentDeadline`, `CreatedAt`, `UpdatedAt`. |
| **Child Entities** | `PricingSnapshot` VO, `CancellationInfo` VO, `BookingStatusChange` | `BookingLineItem` (table `BookingLineItems`, `Type`: `Rent`/`Deposit`/`CleaningFee`/`OverKmFee`, `Amount`), `CancellationPolicySnapshot` (table `CancellationPolicySnapshots`, 1:1, `PolicyDetails` JSONB), `BookingStatusHistory` (table `BookingStatusHistories`, `Status`, `ChangedAt`, `Reason`). |
| **State Machine** | `Requested` -> `AwaitingPayment` -> `Confirmed` | `Pending` ──Approve──► `Confirmed` ──MarkPaid──► `Paid` ──StartTrip──► `InProgress` ──CompleteTrip──► `Completed`. |
| **Published Events** | 8 events (including `TripStarted`, `TripCompleted`, `BookingApproved`) | **7 published events**: `BookingRequested`, `BookingConfirmed`, `BookingPaid`, `BookingRejected`, `BookingCancelled`, `BookingExpired`, `BookingCompleted`. |
| **Contracts** | `IBookingsApi.GetBookingSummaryAsync` | `IBookingApi.IsParticipantAsync(bookingId, userId)`. |
| **Table Naming** | snake_case tables | Retained snake_case tables (`bookings`, `booking_line_items`, `cancellation_policy_snapshots`, `booking_status_histories`) matching PostgreSQL conventions. |

---

## 1. Objectives & Scope
The Bookings module is the central orchestrator of the rental lifecycle. It manages booking requests, owner acceptance, payment status confirmation, trip execution, and cancellation rules with an immutable price and policy snapshot.

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after Phase 1 contracts are available
git checkout -b feat/module-bookings

# Build module
dotnet build src/Modules/Bookings/CarRental.Modules.Bookings/CarRental.Modules.Bookings.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks` (Money, DateTimeRange, Result, Guard, AggregateRoot, Entity).
- **Synchronous Upstream Contracts:**
  - `IUsersApi.GetBookingEligibilityAsync` (`Users.Contracts`)
  - `IFleetApi.GetVehicleSummaryAsync` (`Fleet.Contracts`)
  - `IPricingApi.GetQuoteAsync` (`Pricing.Contracts`)
  - `IAvailabilityApi.PlaceHoldAsync`, `ConfirmHoldAsync`, & `ReleaseHoldAsync` (`Availability.Contracts`)
- **Consumes Events from:**
  - `Payments`: `PaymentSucceeded` -> `MarkPaid`
  - `Handover`: `HandoverConfirmed` -> `StartTrip` (Pickup) / `CompleteTrip` (Return)
- **Downstream Consumers of Booking Contracts:**
  - `Payments`, `Handover`, `Notifications`, `Reviews`, `Reporting`, `Search`.

---

## 4. Contracts Project (`CarRental.Modules.Bookings.Contracts`)

### 4.1 Synchronous Interface (`IBookingApi`)
```csharp
public interface IBookingApi
{
    Task<bool> IsParticipantAsync(Guid bookingId, Guid userId, CancellationToken ct = default);
}
```

### 4.2 Published Integration Events (7 Published Events)
- `BookingRequested(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, Guid CustomerId, Guid OwnerId, Guid VehicleId, DateTimeRange Period, Money Total, Money DepositAmount)`
- `BookingConfirmed(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, Money PayableAmount, Money DepositPortion, DateTimeOffset PaymentDeadline)`
- `BookingPaid(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, Guid CustomerId, Guid OwnerId, Guid VehicleId, DateTimeRange Period)`
- `BookingRejected(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, Guid OwnerId, string Reason)`
- `BookingCancelled(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, Guid CustomerId, Money PenaltyAmount, Money RefundableAmount, string Reason)`
- `BookingExpired(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, string Reason)`
- `BookingCompleted(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, Guid VehicleId, Guid CustomerId, Guid OwnerId)`

---

## 5. Domain Layer (`Entities/`, `Enums/`)

### 5.1 Enums & Strongly-Typed IDs
- `readonly record struct BookingId(Guid Value)`
- `BookingStatus`: `Pending`, `Confirmed`, `Paid`, `InProgress`, `Completed`, `Cancelled`, `Rejected`, `Expired`.
- `BookingLineItemType`: `Rent`, `Deposit`, `CleaningFee`, `OverKmFee`.

### 5.2 Child Entities
- **`BookingLineItem`** (Table `BookingLineItems`):
  - Properties: `Guid Id`, `Guid BookingId`, `BookingLineItemType Type`, `Money Amount`.
- **`CancellationPolicySnapshot`** (Table `CancellationPolicySnapshots`):
  - Properties: `Guid BookingId` (PK/FK), `string PolicyDetailsJson` (JSONB snapshot of tiers and fee info).
- **`BookingStatusHistory`** (Table `BookingStatusHistories`):
  - Properties: `Guid Id`, `Guid BookingId`, `BookingStatus Status`, `DateTimeOffset ChangedAt`, `string? Reason`.

### 5.3 Aggregate Root: `Booking`
- Table `Bookings`:
  - `Guid Id`
  - `Guid CustomerId` (indexed)
  - `Guid OwnerId` (indexed)
  - `Guid VehicleId` (indexed)
  - `BookingStatus Status`
  - `DateTimeOffset StartTime`
  - `DateTimeOffset EndTime`
  - `string PickupLocation`
  - `DateTimeOffset? PaymentDeadline` [ASSUMPTION: default 2h after approval]
  - `DateTimeOffset CreatedAt`
  - `DateTimeOffset UpdatedAt`
  - Child collections: `IReadOnlyCollection<BookingLineItem> LineItems`, `CancellationPolicySnapshot PolicySnapshot`, `IReadOnlyCollection<BookingStatusHistory> StatusHistory`.
- State Machine:
  ```
  Pending ──Approve──► Confirmed ──MarkPaid──► Paid ──StartTrip──► InProgress ──CompleteTrip──► Completed
     │                    │                     │
     ├─Reject──► Rejected ├─Expire──► Expired   └─Cancel──► Cancelled
     ├─Expire──► Expired  └─Cancel──► Cancelled      (AdminCancel: any non-terminal state)
     └─Cancel──► Cancelled
  ```
- Domain Methods & Invariants:
  ```csharp
  static Result<Booking> Request(Guid customerId, Guid ownerId, Guid vehicleId, DateTimeRange period, string pickupLocation, IReadOnlyList<BookingLineItem> lineItems, CancellationPolicySnapshot policy, DateTimeOffset now);
  Result Approve(Guid ownerId, DateTimeOffset now); // Pending -> Confirmed
  Result Reject(Guid ownerId, string reason, DateTimeOffset now); // Pending -> Rejected
  Result MarkPaid(Guid paymentId, DateTimeOffset now); // Confirmed -> Paid
  Result<CancellationOutcome> Cancel(Guid actorId, string reason, DateTimeOffset now); // customer cancel; calculates penalty from snapshot
  Result AdminCancel(Guid adminId, string reason, DateTimeOffset now);
  Result Expire(DateTimeOffset now); // Pending (hold expired) or Confirmed (payment deadline)
  Result StartTrip(DateTimeOffset now); // Paid -> InProgress (from HandoverConfirmed/Pickup)
  Result CompleteTrip(DateTimeOffset now); // InProgress -> Completed (from HandoverConfirmed/Return)
  Result AddOverKmFee(Money amount); // Appends OverKmFee line item on completion
  ```
- Invariants:
  - `CustomerId != OwnerId`.
  - `StartTime` in the future at request and `EndTime > StartTime`.
  - At least one `Rent` and one `Deposit` line item required.
  - Line items and policy snapshot are strictly immutable after creation (except appending `OverKmFee`).
  - Terminal states: `Completed`, `Cancelled`, `Rejected`, `Expired`.
  - Every transition writes a `BookingStatusHistory` entry and updates `UpdatedAt`.

---

## 6. Application Layer & Orchestration (`Services/`, `Jobs/`)
- `BookingPlacementService`:
  ```csharp
  1. var eligibility = await usersApi.GetBookingEligibilityAsync(renterId);
  2. var vehicle = await fleetApi.GetVehicleSummaryAsync(vehicleId); // must be Published
  3. var quote = await pricingApi.GetQuoteAsync(vehicleId, period);
  4. var holdResult = await availabilityApi.PlaceHoldAsync(vehicleId, bookingId, period, ttl: 12h);
  5. try {
         var booking = Booking.Request(...);
         await repository.AddAsync(booking);
     } catch {
         await availabilityApi.ReleaseHoldAsync(vehicleId, bookingId); // Compensation
         throw;
     }
  ```
- On `Approve`: Application layer calls `IAvailabilityApi.ConfirmHoldAsync(vehicleId, bookingId)` synchronously.
- On `Reject`, `Cancel`, `Expire`: Application layer calls `IAvailabilityApi.ReleaseHoldAsync(vehicleId, bookingId)` synchronously.
- `ExpireOverdueBookingsJob`: Scans for expired `Pending` holds and overdue `Confirmed` payment deadlines.

---

## 7. Persistence & EF Core (`Data/`)
- `BookingDbContext`: `HasDefaultSchema("booking")`.
- Table mapping (snake_case):
  - `bookings` (Aggregate Root with `xmin` row version)
  - `booking_line_items` (FK `booking_id`)
  - `cancellation_policy_snapshots` (PK/FK `booking_id`, JSONB column for `policy_details`)
  - `booking_status_histories` (FK `booking_id`)
## 8. Step-by-Step Task Checklist
- [x] Document updated architecture and database entity mapping.
- [ ] Implement `Booking` aggregate root and child entities (`BookingLineItem`, `CancellationPolicySnapshot`, `BookingStatusHistory`).
- [ ] Implement `IBookingApi` and integration events in `CarRental.Modules.Bookings.Contracts`.
- [ ] Setup `BookingDbContext` with schema `booking`.
- [ ] Add domain unit tests for state machine transitions, cancellation penalty calculation, and invariant guards.
- [ ] Verify zero EF/ASP.NET dependencies in `Entities/` via `DomainEncapsulationTests`.
