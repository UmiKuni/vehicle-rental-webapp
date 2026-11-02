# Car Rental Platform: Master Backend Implementation Plan

> **Author:** Architecture & Backend Engineering Team  
> **Source Design:** [be/docs/domain-model-entities-reworked.md](file:///c:/Users/Nguyen/OneDrive/M%C3%A1y%20t%C3%ADnh/DotNet_class/Do_An/vehicle-rental-webapp/be/docs/domain-model-entities-reworked.md) (Derived 1:1 from `Thiết kế hệ thống, CSDL và API`)  
> **Target Framework:** .NET 9, ASP.NET Core, PostgreSQL 17 + PostGIS, Wolverine 3.8 Outbox, NetArchTest  
> **Scope:** Full master implementation plan covering Shared Kernel and 13 modules across isolated git branches and folders.

> [!IMPORTANT]
> **Active Implementation Scope Constraint (Phase-Level Boundary):**
> Current engineering focus is strictly restricted to **Contracts, Rich Domain Entities, and EF Core Data Configurations (`Data/`)**.
> - **In Scope:** Aggregate roots, child entities, value objects, domain events, strongly-typed IDs, `Contracts` (interfaces, DTOs, enums, integration events), `DbContext`, and `IEntityTypeConfiguration<T>`.
> - **Strictly Out of Scope (Do NOT build yet):** Application services, repository classes, minimal API route endpoints, controllers, and background jobs. Stop proceeding immediately after domain entities and EF Core mappings are completed.
> - **EF Core Migrations Constraint:** Do NOT run migrations during entity construction. Database migrations (`dotnet ef migrations add`) will only be generated after all entities across all modules have been fully implemented and verified.

---

## 0. Notification of Architectural & Entity Model Alignment (Changelog)

Following the formal consolidation of the source requirements (`Thiết kế hệ thống, CSDL và API`) into `domain-model-entities-reworked.md`, the platform data design and integration contracts have been harmonized:

1. **Entity & Table Mapping Harmonization:**
   - Source database tables and column names map 1:1 into rich aggregate roots and child entities using `snake_case` table names (e.g. `users`, `customer_profiles`, `vehicles`, `vehicle_images`, `pricing_plans`, `calendar_entries`, `bookings`, `payments`, `deposits`, `handovers`).
   - Domain logic and business methods (`Approve()`, `Cancel()`, `PlaceHold()`, `Confirm()`) encapsulate all invariants with zero public setters.
2. **Integration Event Catalog Refinement (16 Published Events Total):**
   - The integration event catalog is streamlined to **exactly 16 published integration events** across all 13 modules.
   - Internal state transitions without external module consumers remain **internal domain events** handled strictly within their local module.
3. **Pragmatic Layered Architecture Confirmed:**
   - Each module contains **two projects**: `CarRental.Modules.<Name>.Contracts` (public) and `CarRental.Modules.<Name>` (internal single assembly with `Endpoints/`, `Services/`, `Entities/`, `Data/`, `EventHandlers/`).
   - All modules directly reference the Shared Kernel (`CarRental.BuildingBlocks`).
   - Strict pure domain isolation is enforced: types under `Entities/` maintain zero dependencies on `Microsoft.EntityFrameworkCore` or `Microsoft.AspNetCore`.

---

## 1. Executive Summary & Architectural Invariants

The backend follows a **Modular Monolith** architecture deployed behind a unified API Host (`CarRental.Api`), while strictly preserving independent domain boundaries across 13 modules:

1. **Rich Domain Model:** Aggregates encapsulate all business invariants and state transitions. No anemic entities (`public set;` is forbidden). State changes go through explicit domain methods (`Approve()`, `Cancel()`, `PlaceHold()`, etc.) with private parameterless constructors for EF Core.
2. **Schema-per-Module Isolation:** Each module owns its dedicated PostgreSQL schema (`booking`, `pricing`, `availability`, `payments`, `handover`, `users`, `fleet`, `search`, `reviews`, `support`, `notifications`, `audit`, `reporting`).
3. **No Cross-Schema Foreign Keys:** Modules reference other aggregates by ID only (`Guid` / strongly-typed IDs like `BookingId`, `UserId`). Cross-schema joins and foreign keys are strictly prohibited.
4. **Contracts-Only Module Communication:** Modules may only reference each other's `*.Contracts` project (exposing synchronous `IApi` interfaces, DTOs, and integration events).
5. **Strict Event Publishing Policy:** Exactly 16 published integration events exist platform-wide. Everything else remains internal domain events.
6. **Asynchronous Consistency via Wolverine Outbox:** State changes publish integration events through the transactional outbox in the same database transaction as `SaveChanges`. Consuming handlers must be idempotent.
7. **Hold Lifecycle & Double-Booking Prevention:** Guaranteed via `IAvailabilityApi` hold logic backed by a PostgreSQL `btree_gist` exclusion constraint (`tstzrange`) on `CalendarEntries`.
8. **Decoupled Security Audit Architecture:** The Audit module does **not** subscribe to business integration events. Instead, `Audit.Contracts` exposes `RecordAuditEntry`, dispatched via the transactional outbox by an Application pipeline behavior for commands marked with `IAuditedCommand` and sensitive document access.

---

## 2. Git Branching & Collaboration Strategy

```
main (Production / Stable)
  └── dev (Integration & Architecture Test Verification)
        ├── feat/shared-kernel (Phase 0 - BuildingBlocks)
        ├── feat/module-users (Phase 1)
        ├── feat/module-fleet (Phase 1)
        ├── feat/module-pricing (Phase 1)
        ├── feat/module-availability (Phase 1)
        ├── feat/module-bookings (Phase 2)
        ├── feat/module-payments (Phase 2)
        ├── feat/module-handover (Phase 3)
        ├── feat/module-reviews (Phase 3)
        ├── feat/module-support (Phase 3)
        ├── feat/module-search (Phase 4)
        ├── feat/module-notifications (Phase 4)
        ├── feat/module-audit (Phase 4)
        └── feat/module-reporting (Phase 4)
```

---

## 3. Implementation Phasing & Dependency Matrix

| Phase | Module / Component | Dedicated Git Branch | Local Implementation Plan | Published Events | Synchronous Contracts Provided | Status (Scope: Domain & EF Core Data) |
|---|---|---|---|---|---|---|
| **0** | **Shared Kernel** | `feat/shared-kernel` | [BuildingBlocks Plan](file:///c:/Users/Nguyen/OneDrive/M%C3%A1y%20t%C3%ADnh/DotNet_class/Do_An/vehicle-rental-webapp/be/src/Shared/CarRental.BuildingBlocks/IMPLEMENTATION_PLAN.md) | None | Building blocks, Primitives, Value Objects (`Money`, `DateTimeRange`, `GeoPoint`) | ✅ **Completed** (75 tests passing) |
| **1** | **Users** | `feat/module-users` | [Users Plan](file:///c:/Users/Nguyen/OneDrive/M%C3%A1y%20t%C3%ADnh/DotNet_class/Do_An/vehicle-rental-webapp/be/src/Modules/Users/IMPLEMENTATION_PLAN.md) | **0** | `IUsersApi.GetBookingEligibilityAsync`, `GetContactAsync` | ✅ **Completed** |
| **1** | **Fleet** | `feat/module-fleet` | [Fleet Plan](file:///c:/Users/Nguyen/OneDrive/M%C3%A1y%20t%C3%ADnh/DotNet_class/Do_An/vehicle-rental-webapp/be/src/Modules/Fleet/IMPLEMENTATION_PLAN.md) | **2** (`VehiclePublished`, `VehicleUnpublished`) | `IFleetApi.GetVehicleSummaryAsync` | ✅ **Completed** |
| **1** | **Pricing** | `feat/module-pricing` | [Pricing Plan](file:///c:/Users/Nguyen/OneDrive/M%C3%A1y%20t%C3%ADnh/DotNet_class/Do_An/vehicle-rental-webapp/be/src/Modules/Pricing/IMPLEMENTATION_PLAN.md) | **1** (`PricingUpdated`) | `IPricingApi.GetQuoteAsync` | ✅ **Completed** |
| **1** | **Availability** | `feat/module-availability` | [Availability Plan](file:///c:/Users/Nguyen/OneDrive/M%C3%A1y%20t%C3%ADnh/DotNet_class/Do_An/vehicle-rental-webapp/be/src/Modules/Availability/IMPLEMENTATION_PLAN.md) | **0** | `IAvailabilityApi.PlaceHoldAsync`, `ConfirmHoldAsync`, `ReleaseHoldAsync`, `CheckAvailabilityAsync` | ✅ **Completed** |
| **2** | **Bookings** | `feat/module-bookings` | [Bookings Plan](file:///c:/Users/Nguyen/OneDrive/M%C3%A1y%20t%C3%ADnh/DotNet_class/Do_An/vehicle-rental-webapp/be/src/Modules/Bookings/IMPLEMENTATION_PLAN.md) | **7** (`BookingRequested`, `BookingConfirmed`, `BookingPaid`, `BookingRejected`, `BookingCancelled`, `BookingExpired`, `BookingCompleted`) | `IBookingApi.IsParticipantAsync`, `MarkPaidAsync`, `GetRentalDetailsAsync`, `StartTripAsync`, `CompleteTripAsync` | ✅ **Completed** |
| **2** | **Payments** | `feat/module-payments` | [Payments Plan](file:///c:/Users/Nguyen/OneDrive/M%C3%A1y%20t%C3%ADnh/DotNet_class/Do_An/vehicle-rental-webapp/be/src/Modules/Payments/IMPLEMENTATION_PLAN.md) | **2** (`PaymentSucceeded`, `PaymentFailed`) | `IPaymentsApi.CreateCheckoutAsync`, `FreezeDepositAsync`, `SettleDepositAsync`, `ReleaseDepositAsync` | ✅ **Completed** |
| **3** | **Handover** | `feat/module-handover` | [Handover Plan](file:///c:/Users/Nguyen/OneDrive/M%C3%A1y%20t%C3%ADnh/DotNet_class/Do_An/vehicle-rental-webapp/be/src/Modules/Handover/IMPLEMENTATION_PLAN.md) | **1** (`HandoverConfirmed`) | None | ✅ **Completed** |
| **3** | **Reviews** | `feat/module-reviews` | [Reviews Plan](file:///c:/Users/Nguyen/OneDrive/M%C3%A1y%20t%C3%ADnh/DotNet_class/Do_An/vehicle-rental-webapp/be/src/Modules/Reviews/IMPLEMENTATION_PLAN.md) | **1** (`RatingSummaryUpdated`) | None | ✅ **Completed** |
| **3** | **Support** | `feat/module-support` | [Support Plan](file:///c:/Users/Nguyen/OneDrive/M%C3%A1y%20t%C3%ADnh/DotNet_class/Do_An/vehicle-rental-webapp/be/src/Modules/Support/IMPLEMENTATION_PLAN.md) | **2** (`TicketOpened`, `TicketResolved`) | None | ✅ **Completed** |
| **4** | **Search** | `feat/module-search` | [Search Plan](file:///c:/Users/Nguyen/OneDrive/M%C3%A1y%20t%C3%ADnh/DotNet_class/Do_An/vehicle-rental-webapp/be/src/Modules/Search/IMPLEMENTATION_PLAN.md) | **0** (Read Model) | None | ✅ **Completed** |
| **4** | **Notifications** | `feat/module-notifications` | [Notifications Plan](file:///c:/Users/Nguyen/OneDrive/M%C3%A1y%20t%C3%ADnh/DotNet_class/Do_An/vehicle-rental-webapp/be/src/Modules/Notifications/IMPLEMENTATION_PLAN.md) | **0** (Consumer) | None | ✅ **Completed** |
| **4** | **Audit** | `feat/module-audit` | [Audit Plan](file:///c:/Users/Nguyen/OneDrive/M%C3%A1y%20t%C3%ADnh/DotNet_class/Do_An/vehicle-rental-webapp/be/src/Modules/Audit/IMPLEMENTATION_PLAN.md) | **0** (Cross-cutting) | `RecordAuditEntry` message contract | ✅ **Completed** |
| **4** | **Reporting** | `feat/module-reporting` | [Reporting Plan](file:///c:/Users/Nguyen/OneDrive/M%C3%A1y%20t%C3%ADnh/DotNet_class/Do_An/vehicle-rental-webapp/be/src/Modules/Reporting/IMPLEMENTATION_PLAN.md) | **0** (Read Model) | None | ✅ **Completed** |

---

## 4. End-to-End Business Flow Execution Guide

### Flow 1: Create Booking
```
Renter -> POST /api/bookings
  -> BookingPlacementService
       ├─ IUsersApi.GetBookingEligibilityAsync (Active + FullName + Verified CCCD & GPLX)
       ├─ IFleetApi.GetVehicleSummaryAsync (Verify Published status and OwnerId)
       ├─ IPricingApi.GetQuoteAsync (Compute price snapshot)
       ├─ IAvailabilityApi.PlaceHoldAsync (Place <= 12h hold in CalendarEntries)
       └─ Booking.Request(...) -> Saves Booking in 'Pending' status.
       [Failure Compensation: if Booking.Request fails after hold, call IAvailabilityApi.ReleaseHoldAsync]
  -> Emits BookingRequested -> Wolverine Outbox
  -> Notifications receives BookingRequested -> Alerts Owner.
  -> Reporting receives BookingRequested -> Increments TotalBookings.
```

### Flow 2: Owner Approval & Payment (Synchronous Payment & Instant Update)
```
Owner -> PUT /api/bookings/{id}/approve
  -> Availability.ConfirmHold (Hold -> Booked, clears ExpiresAt)
  -> Booking.Approve -> Status = Confirmed
  -> Emits BookingConfirmed to Wolverine Outbox -> Alerts Renter with checkout link.

Renter pays via POST /api/payments/checkout or Gateway Webhook:
  -> Payments marks Payment(Success) + Creates Deposit(Held)
  -> Synchronously calls IBookingApi.MarkPaidAsync(bookingId, paymentId) -> Booking Status = Paid immediately
  -> Emits PaymentSucceeded to Wolverine Outbox -> Notifications (Receipt) & Reporting (Daily Revenue).
```

### Flow 3: Pickup & Return Handover (Synchronous Verification & Status Progression)
```
Pickup:
  -> POST /api/handovers (Type = Pickup) -> Synchronously checks IBookingApi.GetRentalDetailsAsync -> Status must be Paid
  -> Upload photos (4 corners + dash with hash) -> Condition
  -> Both parties PUT /confirm -> Status = Confirmed
  -> Synchronously calls IBookingApi.StartTripAsync(bookingId) -> Booking Status = InProgress

Return:
  -> POST /api/handovers (Type = Return, Return Odo >= Pickup Odo) -> Upload photos -> Condition
  -> Both parties PUT /confirm -> Status = Confirmed
  -> Synchronously calls IBookingApi.CompleteTripAsync(bookingId, overKmFee) -> Booking Status = Completed
  -> Synchronously calls IPaymentsApi.ReleaseDepositAsync(bookingId) -> Schedules deposit release after inspection
  -> Emits BookingCompleted to Wolverine Outbox -> Reviews (Enables feedback) & Notifications.
```

### Flow 4: Deposit Settlement & Disputes (Synchronous Support -> Payments)
```
Normal flow:
  -> Scheduled job releases Deposit(Held -> Released) after inspection window passes.

Dispute opened:
  -> Support opens ticket -> Synchronously calls IPaymentsApi.FreezeDepositAsync(bookingId) -> IsFrozen = true
  -> Admin resolves dispute -> Support synchronously calls IPaymentsApi.SettleDepositAsync(bookingId, percent)
  -> Payments settles Deposit by ruling (Released / Deducted / Forfeited)
  -> Emits TicketResolved to Wolverine Outbox -> Alerts customer and owner via Notifications.
```

---

## 5. Master Integration Event Catalog (Decoupled Side-Effects & Read Projections)

Every integration event is an immutable `record` with `EventId`, `OccurredAt`, and primitive IDs. Core business transactions use synchronous in-process `IApi` calls, while messaging is reserved strictly for non-blocking side effects and read projections.

| Published Event | Origin Module | Main Consumers (Asynchronous Side-Effects Only) |
|---|---|---|
| `VehiclePublished` | `Fleet` | Search, Notifications |
| `VehicleUnpublished` | `Fleet` | Search, Notifications |
| `PricingUpdated` | `Pricing` | Search |
| `BookingRequested` | `Booking` | Notifications, Reporting |
| `BookingConfirmed` | `Booking` | Notifications |
| `BookingRejected` | `Booking` | Notifications |
| `BookingCancelled` | `Booking` | Notifications, Reporting |
| `BookingExpired` | `Booking` | Notifications |
| `BookingCompleted` | `Booking` | Reviews, Notifications |
| `PaymentSucceeded` | `Payments` | Notifications, Reporting |
| `PaymentFailed` | `Payments` | Notifications |
| `RatingSummaryUpdated` | `Reviews` | Search |
| `TicketResolved` | `Support` | Notifications |

---

## 6. Architecture Enforcement & Quality Gates

Continuous integration strictly verifies:
- `DomainEncapsulationTests`: Zero EF Core or ASP.NET dependencies in any `Entities/` namespace.
- `ModuleEncapsulationTests`: No cross-module internal references; only `*.Contracts` and `CarRental.BuildingBlocks` allowed.
- `EntityEncapsulationTests`: Domain entities inherit `Entity<TId>` or `AggregateRoot<TId>` and have private setters only.
- `IntegrationEventsTests`: All integration events in `*.Contracts` are immutable records implementing `IIntegrationEvent`.

---

## 7. Current Progress & Phase Delivery Summary (Phases 0–4 Completed)

All domain entities, contracts, strongly-typed IDs, and EF Core data configurations across all 13 modules and Shared Kernel have been implemented and verified.

### 7.1 Delivery Breakdown by Phase
- **Phase 0 (Shared Kernel):** Complete. `Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`, `Result<T>`, `Error`, `DomainException`, value objects (`Money`, `GeoPoint`, `DateTimeRange`, `Address`, `PhoneNumber`, `Rating`, `FileReference`).
- **Phase 1 (Core Identity, Fleet & Catalog):** Complete. `Users` (roles, KYC profiles), `Fleet` (vehicles, approvals, photos), `Pricing` (base/custom rates, plans), `Availability` (calendar entries, hold lifecycle).
- **Phase 2 (Lifecycle & Transactions):** Complete. `Bookings` (aggregates, line items, status history, cancellation snapshot, 7 integration events), `Payments` (payments, held/frozen deposits, webhook inbox deduplication, 2 integration events).
- **Phase 3 (Physical Handover, Feedback & Support):** Complete. `Handover` (contexts, checklists, photo hash inspection, 1 integration event), `Reviews` (reviewable bookings, ratings summaries, 1 integration event), `Support` (tickets, messages, resolution split rulings, 2 integration events).
- **Phase 4 (Platform Intelligence & Read Projections):** Complete. `Search` (PostGIS read model, LWW out-of-order event convergence), `Notifications` (retry limits, delivery deduplication), `Audit` (append-only log, PII masking, `RecordAuditEntry` command), `Reporting` (UTC+7 daily booking/revenue projections, `ProcessedEvent` idempotency).

### 7.2 Verification Metrics
- **Test Suite Pass Rate:** 190 / 190 tests passed (100% green).
  - `CarRental.BuildingBlocks.Tests`: 75 tests passed.
  - `CarRental.Modules.Tests`: 107 tests passed.
  - `CarRental.ArchitectureTests`: 8 tests passed.
- **EF Core Migrations Policy:** 0 migrations generated during entity construction; preserved for global migration phase.

