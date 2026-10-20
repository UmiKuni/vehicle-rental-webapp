# Reporting Module — Implementation Plan

> **Module Directory:** `src/Modules/Reporting`  
> **Projects:** `CarRental.Modules.Reporting.csproj`, `CarRental.Modules.Reporting.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-reporting`  
> **Phase:** 4 (Platform Intelligence & Read Projections — Parallel Stream 4)  
> **PostgreSQL Schema:** `reporting`  
> **References:** [be/docs/domain-model-entities-reworked.md](../../../docs/domain-model-entities-reworked.md) (Section 4.13 & 5)

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
| **Data Entities** | Multiple read models: `DailyPlatformStats`, `VehiclePerformance`, `OwnerEarnings` | Single consolidated read model: `DailyBookingStats` (table `DailyBookingStats`) matching source schema. |
| **Properties** | Multi-dimensional financial ledgers | `Date` (PK, DateOnly / Timestamptz date bucket in UTC+7), `TotalBookings`, `TotalRevenue`, `CancellationCount`. |
| **Idempotency** | Unspecified | Dedicated `processed_events` table (PK `EventId`) to guarantee idempotent projection. |
| **Published Events** | 0 published events | **0 published events**. |
| **Table Naming** | snake_case tables | Retained snake_case tables (`daily_booking_stats`, `processed_events`) matching PostgreSQL conventions. |

---

## 1. Objectives & Scope
The Reporting module maintains pre-aggregated financial and booking performance read models for executive and administrative dashboards (`GET /api/reporting/dashboard`). It projects metrics asynchronously from Booking and Payment integration events.

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after Phase 1-3 contracts are available
git checkout -b feat/module-reporting

# Build module
dotnet build src/Modules/Reporting/CarRental.Modules.Reporting/CarRental.Modules.Reporting.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks` (Money, Result).
- **Consumes Events from:**
  - `Bookings`: `BookingRequested`, `BookingCancelled`
  - `Payments`: `PaymentSucceeded`

---

## 4. Contracts Project (`CarRental.Modules.Reporting.Contracts`)

### 4.1 Synchronous Interface
*(No synchronous interface required; reporting queries are accessed via HTTP GET).*

### 4.2 Published Integration Events
- **0 Published Events** (Reporting is a pure read projection).

---

## 5. Domain Layer (`ReadModels/`)

### 5.1 Read Model: `DailyBookingStats`
- Table `DailyBookingStats`:
  - `DateOnly Date` (Primary Key, UTC+7 day bucket)
  - `int TotalBookings`
  - `decimal TotalRevenue` (VND)
  - `int CancellationCount`
- Domain Projection Methods:
  ```csharp
  bool ApplyBookingRequested(DateTimeOffset occurredAt);
  bool ApplyBookingCancelled(DateTimeOffset occurredAt);
  bool ApplyPaymentSucceeded(decimal amount, DateTimeOffset occurredAt);
  ```

### 5.2 Idempotency Entity: `ProcessedEvent`
- Table `processed_events`:
  - `Guid EventId` (Primary Key)
  - `DateTimeOffset ProcessedAt`

---

## 6. Persistence & EF Core (`Data/`)
- `ReportingDbContext`: `HasDefaultSchema("reporting")`.
- Table mapping (snake_case):
  - `daily_booking_stats` (PK `date`)
  - `processed_events` (PK `event_id`)
## 7. Event Handlers & Endpoints (`EventHandlers/`, `Endpoints/`)
- Integration Event Handlers (Idempotent):
  - `BookingRequestedHandler`: Increments `TotalBookings` for date bucket.
  - `BookingCancelledHandler`: Increments `CancellationCount` for date bucket.
  - `PaymentSucceededHandler`: Adds payment amount to `TotalRevenue`.
- Minimal API Endpoints (`MapGroup("/api/reporting")`):
  - `GET /dashboard` (Admin policy: returns aggregate platform booking and revenue statistics)

---

## 8. Step-by-Step Task Checklist
- [x] Document updated architecture and database entity mapping.
- [ ] Implement `DailyBookingStats` read model and idempotent mutation methods.
- [ ] Setup `ReportingDbContext` with schema `reporting` and `processed_events` table.
- [ ] Implement Wolverine handlers for `BookingRequested`, `BookingCancelled`, and `PaymentSucceeded`.
- [ ] Add unit tests for date bucket aggregation and duplicate event handling.
