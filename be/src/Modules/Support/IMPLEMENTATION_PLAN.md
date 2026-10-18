# Support Module — Implementation Plan

> **Module Directory:** `src/Modules/Support`  
> **Projects:** `CarRental.Modules.Support.csproj`, `CarRental.Modules.Support.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-support`  
> **Phase:** 3 (Physical Fulfillment & Trust — Parallel Stream 3)  
> **PostgreSQL Schema:** `support`  
> **References:** [be/docs/domain-model-entities-updated.md](../../../docs/domain-model-entities-updated.md) (Section 4.10 & 5)

---

## 1. Objectives & Scope
The Support module handles customer inquiries, tickets with internal team notes, and binding booking dispute arbitrations (damage compensation, late returns, fuel discrepancies, security deposit deductions).

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after Phase 2 contracts are available
git checkout -b feat/module-support

# Build module
dotnet build src/Modules/Support/CarRental.Modules.Support/CarRental.Modules.Support.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks` (Money, FileReference, Result, Guard, AggregateRoot).
- **Consumes Events from:**
  - `Bookings`: `TripCompleted` (allows opening a dispute within 72 hours).
  - `Handover`: `ReturnCompleted` (if `TripSummary.NewDamages` is not empty, flags/suggests dispute claim).
- **Downstream Consumers of Support Contracts:**
  - `Payments`: Consumes `DisputeOpened` to freeze owner payout, `DisputeResolved` to apply refunds or penalty charges.
  - `Notifications`, `Reporting`.

---

## 4. Contracts Project (`CarRental.Modules.Support.Contracts`)
### 4.1 Synchronous Interface (`ISupportApi`)
```csharp
public interface ISupportApi
{
    Task<bool> HasActiveDisputeAsync(Guid bookingId, CancellationToken ct = default);
}
```

### 4.2 Published Integration Events (2 Published Events)
- `DisputeOpened(Guid EventId, DateTimeOffset OccurredAt, Guid DisputeId, Guid BookingId, Guid OpenedBy, Guid AgainstUserId, string Reason, Money? ClaimedAmount)`
- `DisputeResolved(Guid EventId, DateTimeOffset OccurredAt, Guid DisputeId, Guid BookingId, DisputeResolutionDto Resolution)`

*(Note: `TicketOpened` and `TicketResolved` are internal domain events; ticket interactions are served via real-time SignalR).*

### 4.3 DTOs
- `DisputeResolutionDto(string Outcome, Money RefundToRenter, Money ChargeToRenter, Money PayoutAdjustment, string Notes, Guid DecidedBy)`

---

## 5. Domain Layer (`Entities/`, `Enums/`, `ValueObjects/`)

### 5.1 Enums & Value Objects
- `TicketCategory` (`Payment`, `Vehicle`, `Owner`, `Renter`, `Account`, `Other`)
- `TicketPriority` (`Low`, `Normal`, `High`, `Urgent`)
- `TicketStatus` (`Open`, `InReview`, `WaitingForCustomer`, `Resolved`, `Closed`)
- `DisputeReason` (`Damage`, `LateReturn`, `NoShow`, `FuelOrMileage`, `Other`)
- `DisputeStatus` (`Opened`, `UnderReview`, `Resolved`, `Withdrawn`)
- `DisputeOutcome` (`FavorOpener`, `FavorOpponent`, `Split`)
- `DisputeResolution(DisputeOutcome Outcome, Money RefundToRenter, Money ChargeToRenter, Money PayoutAdjustment, string Notes, Guid DecidedBy, DateTimeOffset DecidedAt)`

### 5.2 Aggregate Root: `SupportTicket`
- Properties: `string Number` (e.g. `TK-10023`), `Guid CreatedBy`, `Guid? BookingId`, `TicketCategory Category`, `TicketPriority Priority`, `TicketStatus Status`, `Guid? AssignedTo`, `List<TicketMessage> Messages`, `DateTimeOffset? ResolvedAt`.
- Methods: `Open(...)`, `Assign(adminId)`, `AddMessage(authorId, role, body, attachments, isInternalNote)`, `Resolve(adminId)`, `Reopen()`, `Close()`.
- Rules:
  - Customers cannot view messages with `IsInternalNote = true`.
  - Admin message automatically transitions status to `WaitingForCustomer`.

### 5.3 Aggregate Root: `Dispute`
- Properties:
  - `Guid BookingId`, `Guid OpenedBy`, `Guid AgainstUserId`, `DisputeReason Reason`, `Money? ClaimedAmount`, `List<EvidenceItem> Evidence`, `DisputeStatus Status`, `DisputeResolution? Resolution`.
- Invariants & Methods:
  - Each booking has at most one unresolved dispute.
  - Allowed window: within 72 hours of `TripCompleted`.
  - Evidence can only be submitted by booking parties or admins.
  - `Resolve(DisputeResolution resolution, DateTimeOffset now)`: Requires `UnderReview` and valid decision maker.

---

## 6. Persistence & EF Core (`Data/`)
- `SupportDbContext`: `HasDefaultSchema("support")`.
- `IEntityTypeConfiguration<SupportTicket>`:
  - Unique index on `Number`.
- `IEntityTypeConfiguration<Dispute>`:
  - Index on `BookingId`.
  - Owned entity for `DisputeResolution`.
- Migration: `AddSupportInitialMigration`.

---

## 7. Event Handlers & Endpoints (`EventHandlers/`, `Endpoints/`)
- Minimal API Endpoints (`MapGroup("/api/support")`):
  - `POST /tickets`
  - `GET /tickets/{id}`
  - `POST /tickets/{id}/messages`
  - `POST /disputes`
  - `POST /disputes/{id}/evidence`
  - Admin endpoints:
    - `POST /admin/disputes/{id}/under-review`
    - `POST /admin/disputes/{id}/resolve`

---

## 8. Step-by-Step Task Checklist
- [ ] Implement Enums, `TicketMessage`, `EvidenceItem`, `DisputeResolution`.
- [ ] Implement `SupportTicket` aggregate root.
- [ ] Implement `Dispute` aggregate root with resolution invariants.
- [ ] Implement `ISupportApi` and integration events in `CarRental.Modules.Support.Contracts`.
- [ ] Configure `SupportDbContext` and EF Core mappings.
- [ ] Create EF Core migration for `support` schema.
- [ ] Build Minimal API endpoints.
- [ ] Unit tests:
  - `Ticket_AddAdminMessage_TransitionsToWaitingForCustomer`
  - `Dispute_Open_WhenPriorUnresolvedDisputeExists_ShouldFail`
  - `Dispute_Resolve_WhenNotInUnderReview_ShouldFail`
- [ ] Testcontainers integration tests.
