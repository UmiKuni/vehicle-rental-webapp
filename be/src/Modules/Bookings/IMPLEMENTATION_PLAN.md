# Bookings Module — Implementation Plan

> **Module Directory:** `src/Modules/Bookings`  
> **Projects:** `CarRental.Modules.Bookings.csproj`, `CarRental.Modules.Bookings.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-bookings`  
> **Phase:** 2 (Rental Flow Core — Parallel Stream 1)  
> **PostgreSQL Schema:** `booking`  
> **References:** [be/docs/domain-model-entities-updated.md](../../../docs/domain-model-entities-updated.md) (Section 4.1, 5, 6)

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
- **Shared Kernel:** `CarRental.BuildingBlocks` (Money, DateTimeRange, Address, Result, Guard, AggregateRoot).
- **Synchronous Upstream Contracts:**
  - `IUsersApi.GetRenterEligibilityAsync` (`Users.Contracts`)
  - `IFleetApi.GetVehicleSummaryAsync` (`Fleet.Contracts`)
  - `IPricingApi.GetQuoteAsync` & `ConsumeQuoteAsync` (`Pricing.Contracts`)
  - `IAvailabilityApi.PlaceHoldAsync`, `ExtendHoldAsync`, & `ReleaseHoldAsync` (`Availability.Contracts`)
- **Consumes Events from:**
  - `Payments`: `PaymentSucceeded` -> `MarkPaid`
  - `Handover`: `PickupCompleted` -> `StartTrip`, `ReturnCompleted` -> `CompleteTrip`
- **Downstream Consumers of Booking Contracts:**
  - `Payments`, `Availability`, `Handover`, `Pricing`, `Notifications`, `Reviews`, `Reporting`, `Search`.

---

## 4. Contracts Project (`CarRental.Modules.Bookings.Contracts`)
### 4.1 Synchronous Interface (`IBookingsApi`)
```csharp
public interface IBookingsApi
{
    Task<Result<BookingSummaryDto>> GetBookingSummaryAsync(Guid bookingId, CancellationToken ct = default);
}
```

### 4.2 DTOs
- `BookingSummaryDto(Guid BookingId, string Code, Guid RenterId, Guid OwnerId, Guid VehicleId, string Status, DateTimeRange Period, Money Total, Money DepositAmount)`

### 4.3 Integration Events
- `BookingRequested(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, string Code, Guid RenterId, Guid OwnerId, Guid VehicleId, DateTimeRange Period, Money Total, Money DepositAmount)`
- `BookingApproved(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, Guid OwnerId, DateTimeOffset PaymentDeadline)`
- `BookingRejected(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, Guid OwnerId, string Reason)`
- `BookingConfirmed(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, Guid PaymentId, Guid VehicleId, Guid RenterId, Guid OwnerId, DateTimeRange Period)`
- `BookingCancelled(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, string CancelledBy, Guid ActorId, string Reason, decimal RefundPercent)`
- `BookingExpired(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, string ExpiryStage)`
- `TripStarted(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId)`
- `TripCompleted(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, Guid VehicleId, Guid RenterId, Guid OwnerId)`

---

## 5. Domain Layer (`Entities/`, `Enums/`, `ValueObjects/`)

### 5.1 Enums & Strongly-Typed IDs
- `readonly record struct BookingId(Guid Value)`
- `BookingStatus` (`Requested`, `AwaitingPayment`, `Confirmed`, `InProgress`, `Completed`, `Rejected`, `Expired`, `Cancelled`)
- `CancelledBy` (`Renter`, `Owner`, `System`, `Admin`)

### 5.2 Value Objects & Child Entities
- `PricingSnapshot`: Immutable snapshot of lines, subtotal, total, deposit, platform fee, owner earning, promo code.
- `CancellationInfo(CancelledBy By, Guid ActorId, string Reason, DateTimeOffset At, decimal RefundPercent)`
- `BookingStatusChange` (Child Entity): `BookingStatus From`, `BookingStatus To`, `DateTimeOffset At`, `Guid ActorId`, `string? Note` (Append-only).

### 5.3 Aggregate Root: `Booking`
- State Machine:
  ```
  Requested ──Approve──► AwaitingPayment ──MarkPaid──► Confirmed ──StartTrip──► InProgress ──CompleteTrip──► Completed
     │                        │                            │
     ├─Reject──► Rejected     ├─ExpirePayment──► Expired   └─Cancel──► Cancelled
     ├─ExpireRequest► Expired └─Cancel──► Cancelled
     └─Cancel (renter)──► Cancelled
  ```
- Public Business Methods:
  - `static Result<Booking> Request(...)`
  - `Result Approve(Guid ownerId, DateTimeOffset now)`
  - `Result Reject(Guid ownerId, string reason, DateTimeOffset now)`
  - `Result MarkPaid(Guid paymentId, Money paid, DateTimeOffset now)`
  - `Result Cancel(CancelledBy by, Guid actorId, string reason, DateTimeOffset now)`
  - `Result Expire(DateTimeOffset now)`
  - `Result StartTrip(DateTimeOffset now)`
  - `Result CompleteTrip(DateTimeOffset now)`
- Invariants:
  - Minimum trip duration 4 hours, maximum 30 days.
  - `RenterId != OwnerId`.
  - `Approve`/`Reject` executable only by `OwnerId` before `OwnerResponseDeadline`.
  - `Pricing` is completely locked and immutable upon creation.
  - `Completed`, `Cancelled`, `Rejected`, `Expired` are terminal states.

---

## 6. Application Layer & Orchestration (`Services/`, `Jobs/`)
- `BookingPlacementService`:
  ```csharp
  // Orchestrated Placement with Compensation:
  1. var eligibility = await usersApi.GetRenterEligibilityAsync(renterId);
  2. var quote = await pricingApi.GetQuoteAsync(vehicleId, renterId, period, promo);
  3. var holdResult = await availabilityApi.PlaceHoldAsync(vehicleId, bookingId, period, ttl: 12h);
  4. try {
         var booking = Booking.Request(...);
         await repository.AddAsync(booking);
         await pricingApi.ConsumeQuoteAsync(quote.Id);
     } catch {
         await availabilityApi.ReleaseHoldAsync(bookingId); // Compensation
         throw;
     }
  ```
- **Hold Lifecycle Management:**
  - Booking owns every deadline.
  - On `Approve`: Application layer calls `IAvailabilityApi.ExtendHoldAsync(holdId, PaymentDeadline)` to synchronize hold with the payment window.
  - On `Reject`, `Cancel`, or `Expire`: Application layer calls `IAvailabilityApi.ReleaseHoldAsync(bookingId)` (Availability also runs `ExpireHoldsJob` as a safety net).
- `ExpireOverdueBookingsJob`: Scheduled cron job scanning for expired `Requested` (past 12h) and `AwaitingPayment` (past 2h) records -> invokes `Expire()`.

---

## 7. Persistence & EF Core (`Data/`)
- `BookingDbContext`: `HasDefaultSchema("booking")`.
- `IEntityTypeConfiguration<Booking>`:
  - Concurrency token `xmin`.
  - Unique index on `Code`.
  - Owned entities for `Period`, `PickupAddress`, `ReturnAddress`, `PricingSnapshot`, `CancellationInfo`.
  - Relation mapping for `History` collection.
- Migration: `AddBookingsInitialMigration`.

---

## 8. Step-by-Step Task Checklist
- [ ] Implement `BookingId`, `BookingStatus`, `CancelledBy` enums.
- [ ] Implement `PricingSnapshot`, `CancellationInfo` value objects and `BookingStatusChange` entity.
- [ ] Implement `Booking` aggregate root with full state machine and domain events.
- [ ] Define `IBookingsApi`, DTOs, and integration events in `CarRental.Modules.Bookings.Contracts`.
- [ ] Configure `BookingDbContext`, EF Core mappings, and initial migration.
- [ ] Implement `BookingPlacementService` with compensation pattern on failure.
- [ ] Implement `ExpireOverdueBookingsJob`.
- [ ] Implement Wolverine event consumers (`PaymentSucceededHandler`, `PickupCompletedHandler`, etc.).
- [ ] Build Minimal API endpoints.
- [ ] Unit tests for `Booking`:
  - `Approve_ByNonOwner_ShouldFail`
  - `Approve_After12HourDeadline_ShouldFail`
  - `MarkPaid_WithInsufficientDeposit_ShouldFail`
  - `Cancel_ComputesCorrectRefundPercentageFromPolicy`
  - `InvalidStateTransition_ThrowsFailureResult`
- [ ] Integration tests using Testcontainers PostgreSQL.
