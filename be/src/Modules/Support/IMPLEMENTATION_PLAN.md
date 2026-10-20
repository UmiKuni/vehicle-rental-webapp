# Support Module — Implementation Plan

> **Module Directory:** `src/Modules/Support`  
> **Projects:** `CarRental.Modules.Support.csproj`, `CarRental.Modules.Support.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-support`  
> **Phase:** 3 (Physical Fulfillment & Trust — Parallel Stream 3)  
> **PostgreSQL Schema:** `support`  
> **References:** [be/docs/domain-model-entities-reworked.md](../../../docs/domain-model-entities-reworked.md) (Section 4.10 & 5)

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
| **Aggregate Root** | `Dispute` aggregate root with complex arbitration splits | `SupportTicket` (table `SupportTickets`) with `Id`, `BookingId`, `ReporterId`, `Status` (`Open`/`InProgress`/`Resolved`), `DepositToCustomerPercent` (0..100), `ResolutionNote`, `ResolvedBy`, `ResolvedAt`. |
| **Child Entities** | `DisputeEvidence`, `DisputeMessage` | `TicketMessage` (table `TicketMessages`, `Id`, `SenderId`, `Content`, `AttachmentKey`). |
| **Deposit Resolution** | Multi-account ledger debit/credit | Single authoritative `DepositToCustomerPercent` (0..100) determining the deposit refund vs forfeiture ruling. |
| **Published Events** | `DisputeOpened`, `DisputeResolved` | `TicketOpened` (triggers deposit freeze in Payments), `TicketResolved` (triggers deposit settlement). |
| **Table Naming** | snake_case tables | Retained snake_case tables (`support_tickets`, `ticket_messages`) matching PostgreSQL conventions. |

---

## 1. Objectives & Scope
The Support module handles customer and owner issue ticketing, incident claims following vehicle return, and administrative dispute arbitration. Admin rulings establish the percentage of the held deposit refunded to the customer versus forfeited/awarded to the owner.

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
- **Shared Kernel:** `CarRental.BuildingBlocks` (Result, Guard, AggregateRoot, Entity).
- **Synchronous Upstream Contracts:**
  - `IBookingApi.IsParticipantAsync` (`Bookings.Contracts`)
- **Downstream Consumers of Support Contracts:**
  - `Payments`: Consumes `TicketOpened` to freeze deposit, and `TicketResolved` to execute financial settlement.
  - `Notifications`: Consumes `TicketResolved` to alert both parties.

---

## 4. Contracts Project (`CarRental.Modules.Support.Contracts`)

### 4.1 Synchronous Interface
*(No synchronous interface required; interactions are event-driven via Wolverine Outbox).*

### 4.2 Published Integration Events (2 Published Events)
- `TicketOpened(Guid EventId, DateTimeOffset OccurredAt, Guid TicketId, Guid BookingId, Guid ReporterId)`
- `TicketResolved(Guid EventId, DateTimeOffset OccurredAt, Guid TicketId, Guid BookingId, int DepositToCustomerPercent, string Note, Guid AdminId)`

---

## 5. Domain Layer (`Entities/`, `Enums/`)

### 5.1 Enums & Strongly-Typed IDs
- `readonly record struct TicketId(Guid Value)`
- `TicketStatus`: `Open`, `InProgress`, `Resolved`.

### 5.2 Child Entity: `TicketMessage`
- Table `TicketMessages`:
  - `Guid Id`
  - `Guid SupportTicketId`
  - `Guid SenderId`
  - `string Content`
  - `string? AttachmentKey`
  - `DateTimeOffset CreatedAt`

### 5.3 Aggregate Root: `SupportTicket`
- Table `SupportTickets`:
  - `Guid Id`
  - `Guid BookingId` (indexed)
  - `Guid ReporterId` (indexed)
  - `TicketStatus Status` (`Open`/`InProgress`/`Resolved`)
  - `int? DepositToCustomerPercent` (0..100) [ASSUMPTION]
  - `string? ResolutionNote`
  - `Guid? ResolvedBy`
  - `DateTimeOffset? ResolvedAt`
  - `DateTimeOffset CreatedAt`
  - Child collection: `IReadOnlyCollection<TicketMessage> Messages`.
- Domain Methods & Invariants:
  ```csharp
  static Result<SupportTicket> Open(Guid bookingId, Guid reporterId, string firstMessage, string? attachmentKey, DateTimeOffset now); // Open, raises TicketOpened
  Result AddMessage(Guid senderId, string content, string? attachmentKey, DateTimeOffset now);
  Result StartReview(Guid adminId); // Open -> InProgress
  Result Resolve(Guid adminId, int depositToCustomerPercent, string note, DateTimeOffset now); // InProgress -> Resolved, raises TicketResolved
  ```
- Invariants:
  - Reporter must be a verified booking participant (`IBookingApi`).
  - No new messages allowed once ticket reaches `Resolved` status.
  - Only an Admin can resolve a ticket.
  - `DepositToCustomerPercent` must be between 0 and 100.
  - At most one unresolved ticket per `(BookingId, ReporterId)`.

---

## 6. Persistence & EF Core (`Data/`)
- `SupportDbContext`: `HasDefaultSchema("support")`.
- Table mapping (snake_case):
  - `support_tickets` (Aggregate Root with `xmin` row version)
  - `ticket_messages` (FK `support_ticket_id`)
## 7. Application Services & Endpoints (`Services/`, `Endpoints/`)
- Minimal API Endpoints (`MapGroup("/api/support/tickets")`):
  - `POST /` (open new ticket)
  - `POST /{id}/messages` (add conversation message/attachment)
  - `PUT /{id}/resolve` (Admin policy: resolve ruling and deposit split)

---

## 8. Step-by-Step Task Checklist
- [x] Document updated architecture and database entity mapping.
- [ ] Implement `SupportTicket` aggregate root and `TicketMessage` entity.
- [ ] Implement `TicketOpened` and `TicketResolved` events in `CarRental.Modules.Support.Contracts`.
- [ ] Setup `SupportDbContext` with schema `support`.
- [ ] Add domain unit tests for ticket state transitions, percent validation, and immutability after resolution.
- [ ] Verify zero EF/ASP.NET dependencies in `Entities/` via `DomainEncapsulationTests`.
