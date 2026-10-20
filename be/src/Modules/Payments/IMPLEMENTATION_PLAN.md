# Payments Module — Implementation Plan

> **Module Directory:** `src/Modules/Payments`  
> **Projects:** `CarRental.Modules.Payments.csproj`, `CarRental.Modules.Payments.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-payments`  
> **Phase:** 2 (Rental Flow Core — Parallel Stream 2)  
> **PostgreSQL Schema:** `payments`  
> **References:** [be/docs/domain-model-entities-reworked.md](../../../docs/domain-model-entities-reworked.md) (Section 4.4, 5, 6)

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
| **Aggregate Roots** | `Payment` (with `PaymentAttempt`, `ManualReceipt`, `Refund`) and `OwnerPayout` aggregate root | Two distinct aggregate roots: `Payment` (table `Payments`) and `Deposit` (table `Deposits`). No separate `OwnerPayout` table in source schema. |
| **Payment Statuses** | `Pending`, `Succeeded`, `Failed`, `Expired`, `Cancelled`, `PartiallyRefunded`, `Refunded` | `PaymentStatus`: `Pending`, `Success`, `Failed`. One single payment covers rent + deposit portion. |
| **Deposit Lifecycle** | Embedded in booking / payout | Dedicated aggregate root `Deposit` (table `Deposits`) with statuses `Held`, `Released`, `Deducted`, `Forfeited`, `IsFrozen`, `ReleaseAfter`. |
| **Webhook Idempotency** | In-memory / generic index | Infrastructure entity `WebhookInbox` (table `WebhookInboxes`, `Id` text, `ProcessedAt`, `Payload` JSONB). |
| **Published Events** | 5 events (`PaymentSucceeded`, `PaymentFailed`, `RefundIssued`, `PayoutScheduled`, `PayoutPaid`) | **2 published events**: `PaymentSucceeded`, `PaymentFailed`. |
| **Table Naming** | snake_case tables | Retained snake_case tables (`payments`, `deposits`, `webhook_inboxes`) matching PostgreSQL conventions. |

---

## 1. Objectives & Scope
The Payments module manages transaction checkout, payment gateway integrations (VNPay, MoMo, Bank Transfer), webhook deduplication via `WebhookInbox`, security deposit custody (`Deposit` aggregate), deposit freeze on dispute, and automated deposit release/deduction rulings.

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after Phase 1 contracts are available
git checkout -b feat/module-payments

# Build module
dotnet build src/Modules/Payments/CarRental.Modules.Payments/CarRental.Modules.Payments.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks` (Money, Result, Guard, AggregateRoot, Entity).
- **Consumes Events from:**
  - `Bookings`:
    - `BookingConfirmed` -> Creates `Payment.CreatePending`.
    - `BookingExpired`, `BookingRejected` -> `Cancel` pending payment.
    - `BookingCancelled` -> `Cancel` pending payment, or if paid deduct penalty from deposit.
    - `BookingCompleted` -> Sets `Deposit.ReleaseAfter` (default 48h after trip end).
  - `Support`:
    - `TicketOpened` -> `Deposit.Freeze` (prevents automated release).
    - `TicketResolved` -> `Deposit.SettleByRuling` (resolves customer/owner split).
- **Downstream Consumers of Payments Contracts:**
  - `Bookings`: Consumes `PaymentSucceeded` to transition to `Paid`.
  - `Notifications`, `Reporting`: Consume `PaymentSucceeded` and `PaymentFailed`.

---

## 4. Contracts Project (`CarRental.Modules.Payments.Contracts`)

### 4.1 Synchronous Interface
*(No synchronous contract required; all interactions are event-driven via Wolverine Outbox).*

### 4.2 Published Integration Events (2 Published Events)
- `PaymentSucceeded(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, Guid PaymentId, Money Amount)`
- `PaymentFailed(Guid EventId, DateTimeOffset OccurredAt, Guid BookingId, Guid PaymentId, string Reason)`

---

## 5. Domain Layer (`Entities/`, `Enums/`)

### 5.1 Enums & Strongly-Typed IDs
- `readonly record struct PaymentId(Guid Value)`
- `readonly record struct DepositId(Guid Value)`
- `PaymentStatus`: `Pending`, `Success`, `Failed`.
- `PaymentMethod`: `VNPay`, `Momo`, `Transfer`.
- `DepositStatus`: `Held`, `Released`, `Deducted`, `Forfeited`.

### 5.2 Aggregate Root: `Payment`
- Table `Payments`:
  - `Guid Id`
  - `Guid BookingId` (indexed)
  - `Money Amount`
  - `PaymentStatus Status` (`Pending`/`Success`/`Failed`)
  - `string? GatewayTransactionId` (unique when set)
  - `PaymentMethod Method` (`VNPay`/`Momo`/`Transfer`)
  - `string? FailureReason` [ASSUMPTION]
  - `Money DepositPortion` [ASSUMPTION: part of Amount representing refundable deposit]
- Domain Methods & Invariants:
  ```csharp
  static Result<Payment> CreatePending(Guid bookingId, Money amount, Money depositPortion, DateTimeOffset now);
  Result Checkout(PaymentMethod method, IPaymentGateway gateway, DateTimeOffset now);
  Result MarkSuccess(string gatewayTransactionId, DateTimeOffset now); // Pending -> Success, raises PaymentSucceeded
  Result MarkFailed(string gatewayTransactionId, string reason, DateTimeOffset now); // Pending -> Failed, raises PaymentFailed
  Result Cancel(string reason, DateTimeOffset now); // Pending -> Failed
  ```
- Invariants:
  - `Amount > 0`.
  - `Success` or `Failed` transitions only valid from `Pending`.
  - At most one `Success` payment per booking.
  - `GatewayTransactionId` unique when set.

### 5.3 Aggregate Root: `Deposit`
- Table `Deposits`:
  - `Guid Id`
  - `Guid BookingId` (indexed)
  - `Money Amount`
  - `DepositStatus Status` (`Held`/`Released`/`Deducted`/`Forfeited`)
  - `Money DeductedAmount` [ASSUMPTION]
  - `DateTimeOffset? ReleaseAfter` [ASSUMPTION: scheduled auto-release timestamp]
  - `bool IsFrozen` [ASSUMPTION: set when a dispute ticket is opened]
  - `DateTimeOffset? SettledAt` [ASSUMPTION]
- Domain Methods & Invariants:
  ```csharp
  static Deposit Hold(Guid bookingId, Money amount); // Created upon PaymentSucceeded
  void Freeze(); // Called on TicketOpened (halts auto-release)
  Result Release(DateTimeOffset now); // Held -> Released
  Result Deduct(Money amount, DateTimeOffset now); // Held -> Deducted (remainder refunded)
  Result Forfeit(DateTimeOffset now); // Held -> Forfeited
  Result SettleByRuling(int depositToCustomerPercent, DateTimeOffset now); // 100 => Released, 1..99 => Deducted, 0 => Forfeited
  ```
- Invariants:
  - Transitions only from `Held`.
  - Deductions can never exceed `Amount`.
  - A frozen deposit (`IsFrozen == true`) is never auto-released.

### 5.4 Infrastructure Entity: `WebhookInbox`
- Table `WebhookInboxes`:
  - `string Id` (Primary Key: gateway transaction reference / event ID)
  - `DateTimeOffset ProcessedAt`
  - `string PayloadJson` (JSONB)
- Invariant: First insert ID before processing; duplicate ID returns HTTP 200 immediately (BR-13 idempotent webhook processing).

---

## 6. Application Services & Background Jobs (`Services/`, `Jobs/`)
- `IPaymentGateway`: Gateway adapter abstraction for VNPay, MoMo, and manual Transfer.
- `ReleaseDueDepositsJob`: Background job running periodically; releases deposits of completed bookings where `now >= ReleaseAfter` and `!IsFrozen`.

---

## 7. Persistence & EF Core (`Data/`)
- `PaymentsDbContext`: `HasDefaultSchema("payments")`.
- Table mapping (snake_case):
  - `payments` (Aggregate Root with `xmin` row version)
  - `deposits` (Aggregate Root with `xmin` row version)
  - `webhook_inboxes` (PK `id`)
- Unique index on `GatewayTransactionId` (where not null).

---

## 8. Step-by-Step Task Checklist
- [x] Document updated architecture and database entity mapping.
- [ ] Implement `Payment` and `Deposit` aggregate roots and `WebhookInbox` entity.
- [ ] Implement integration events in `CarRental.Modules.Payments.Contracts`.
- [ ] Setup `PaymentsDbContext` with schema `payments`.
- [ ] Implement idempotent webhook handler with `WebhookInbox`.
- [ ] Implement `ReleaseDueDepositsJob` and dispute freeze/ruling handlers.
- [ ] Add domain unit tests for payment state machine, deposit deduction invariants, and ruling settlements.
- [ ] Verify zero EF/ASP.NET dependencies in `Entities/` via `DomainEncapsulationTests`.
