# Notifications Module — Implementation Plan

> **Module Directory:** `src/Modules/Notifications`  
> **Projects:** `CarRental.Modules.Notifications.csproj`, `CarRental.Modules.Notifications.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-notifications`  
> **Phase:** 4 (Platform Intelligence & Read Projections — Parallel Stream 2)  
> **PostgreSQL Schema:** `notifications`  
> **References:** [be/docs/domain-model-entities-reworked.md](../../../docs/domain-model-entities-reworked.md) (Section 4.11 & 5)

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
| **Aggregate Root** | Multi-channel user preference entity + notification queue | `Notification` (table `Notifications`) with `Id`, `UserId`, `Type` (`Email`/`SMS`/`Push`), `Content` (JSONB), `Status` (`Pending`/`Sent`/`Failed`), `Attempts`, `LastError`, `CreatedAt`, `SentAt`, `DedupKey`. |
| **Deduplication** | Application-level memory cache | Explicit `DedupKey` column with unique database index to guarantee at-most-once delivery on event redelivery. |
| **Recipient Contact Resolution** | Mixed in payload | Synchronously calls `IUsersApi.GetContactAsync(userId)` to obtain email and phone numbers. |
| **Published Events** | 0 published events | **0 published events**. |
| **Table Naming** | snake_case tables | Retained snake_case tables (`notifications`) matching PostgreSQL conventions. |

---

## 1. Objectives & Scope
The Notifications module delivers transactional alerts via Email, SMS, and Push notifications across key rental milestones (booking requests, approvals, payment receipts, disputes, and publication decisions). It encapsulates delivery retries and deduplication.

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after Phase 1-3 contracts are available
git checkout -b feat/module-notifications

# Build module
dotnet build src/Modules/Notifications/CarRental.Modules.Notifications/CarRental.Modules.Notifications.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks` (Result, Guard, AggregateRoot, Entity).
- **Synchronous Upstream Contracts:**
  - `IUsersApi.GetContactAsync` (`Users.Contracts`) to retrieve user email and phone numbers.
- **Consumes Events from:**
  - `Bookings`: `BookingRequested`, `BookingConfirmed`, `BookingRejected`, `BookingCancelled`, `BookingExpired`
  - `Payments`: `PaymentSucceeded`, `PaymentFailed`
  - `Support`: `TicketResolved`
  - `Fleet`: `VehiclePublished`, `VehicleUnpublished`

---

## 4. Contracts Project (`CarRental.Modules.Notifications.Contracts`)

### 4.1 Synchronous Interface
*(No synchronous interface required; notification dispatch is entirely event-driven).*

### 4.2 Published Integration Events
- **0 Published Events** (Notifications is a pure consumer).

---

## 5. Domain Layer (`Entities/`, `Enums/`)

### 5.1 Enums & Strongly-Typed IDs
- `readonly record struct NotificationId(Guid Value)`
- `NotificationType`: `Email`, `SMS`, `Push`.
- `NotificationStatus`: `Pending`, `Sent`, `Failed`.

### 5.2 Aggregate Root: `Notification`
- Table `Notifications`:
  - `Guid Id`
  - `Guid UserId` (indexed)
  - `NotificationType Type` (`Email`/`SMS`/`Push`)
  - `string Content` (JSONB payload: template key + data parameters)
  - `NotificationStatus Status` (`Pending`/`Sent`/`Failed`)
  - `int Attempts` [ASSUMPTION]
  - `string? LastError` [ASSUMPTION]
  - `DateTimeOffset CreatedAt`
  - `DateTimeOffset? SentAt`
  - `string DedupKey` (unique, indexed)
- Domain Methods:
  ```csharp
  static Result<Notification> Create(Guid userId, NotificationType type, string content, string dedupKey, DateTimeOffset now);
  void MarkSent(DateTimeOffset now);
  void MarkFailed(string error); // max 3 attempts before permanently marked Failed
  ```

---

## 6. Persistence & EF Core (`Data/`)
- `NotificationsDbContext`: `HasDefaultSchema("notifications")`.
- Table mapping (snake_case):
  - `notifications` (Aggregate Root with `xmin` row version)
- Unique index on `DedupKey`.

---

## 7. Event Handlers & Providers (`EventHandlers/`, `Providers/`)
- External Provider Adapters:
  - `IEmailSender`: SMTP / SendGrid adapter.
  - `ISmsSender`: Twilio / VN SMS provider adapter.
- Handlers subscribe to the 10 consumed events, build the `DedupKey = $"{EventId}:{Type}:{UserId}"`, and dispatch notifications.

---

## 8. Step-by-Step Task Checklist
- [x] Document updated architecture and database entity mapping.
- [ ] Implement `Notification` aggregate root and deduplication invariants.
- [ ] Setup `NotificationsDbContext` with schema `notifications`.
- [ ] Implement event handlers for Booking, Payments, Support, and Fleet events.
- [ ] Add unit tests for deduplication and retry transitions.
