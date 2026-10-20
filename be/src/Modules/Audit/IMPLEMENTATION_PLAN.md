# Audit Module — Implementation Plan

> **Module Directory:** `src/Modules/Audit`  
> **Projects:** `CarRental.Modules.Audit.csproj`, `CarRental.Modules.Audit.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-audit`  
> **Phase:** 4 (Platform Intelligence & Read Projections — Parallel Stream 3)  
> **PostgreSQL Schema:** `audit`  
> **References:** [be/docs/domain-model-entities-reworked.md](../../../docs/domain-model-entities-reworked.md) (Section 4.12 & 5)

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
| **Architecture** | Business event subscriber | Explicit message-driven append-only architecture: receives `RecordAuditEntry` dispatched via outbox by pipeline behaviors for `IAuditedCommand` and sensitive PII access. |
| **Data Entity** | `AuditEntry` with loose fields | `AuditLogEntry` (table `AuditLogEntries`) with `Id`, `Timestamp`, `ActorId`, `IpAddress`, `Action`, `EntityName`, `EntityId`, `BeforeState` (JSONB), `AfterState` (JSONB). |
| **Immutability** | Code-level convention | Strict database security: zero `UPDATE` or `DELETE` permissions granted on the `audit` schema. |
| **Published Events** | 0 published events | **0 published events**. |
| **Table Naming** | snake_case tables | Retained snake_case tables (`audit_log_entries`) matching PostgreSQL conventions. |

---

## 1. Objectives & Scope
The Audit module provides an immutable, tamper-evident historical audit log for compliance, security tracking, administrative actions, and sensitive identity document inspections.

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after Phase 1-3 contracts are available
git checkout -b feat/module-audit

# Build module
dotnet build src/Modules/Audit/CarRental.Modules.Audit/CarRental.Modules.Audit.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks`.
- **Contracts Provided:** Exposes `RecordAuditEntry` command message in `CarRental.Modules.Audit.Contracts`.

---

## 4. Contracts Project (`CarRental.Modules.Audit.Contracts`)

### 4.1 Command Message Contract
```csharp
public record RecordAuditEntry(
    Guid EventId,
    DateTimeOffset Timestamp,
    Guid ActorId,
    string IpAddress,
    string Action,
    string EntityName,
    string EntityId,
    string? BeforeStateJson,
    string? AfterStateJson);
```

### 4.2 Published Integration Events
- **0 Published Events** (Audit is an append-only sink).

---

## 5. Domain Layer (`Entities/`)

### 5.1 Append-Only Entity: `AuditLogEntry`
- Table `AuditLogEntries`:
  - `Guid Id` (Primary Key)
  - `DateTimeOffset Timestamp`
  - `Guid ActorId`
  - `string IpAddress`
  - `string Action` (e.g. `BanUser`, `ApproveVehicle`, `ResolveDispute`, `ViewCitizenIdentity`)
  - `string EntityName`
  - `string EntityId`
  - `string? BeforeState` (JSONB)
  - `string? AfterState` (JSONB)
- Invariants & Encapsulation:
  - Private parameterless constructor.
  - Factory method `AuditLogEntry.Record(...)`.
  - Zero modification or deletion methods.
  - Sanitization rule: PII (full CCCD/GPLX numbers) must be masked prior to recording.

---

## 6. Persistence & EF Core (`Data/`)
- `AuditDbContext`: `HasDefaultSchema("audit")`.
- Table mapping (snake_case):
  - `audit_log_entries` (PK `id`, JSONB columns for `before_state` and `after_state`)
- PostgreSQL database role restrictions: Grant only `SELECT` and `INSERT` on `audit."AuditLogEntries"`.

---

## 7. Message Handlers & Endpoints (`MessageHandlers/`, `Endpoints/`)
- `RecordAuditEntryHandler`: Wolverine message handler appending records via `AuditDbContext`.
- Minimal API Endpoints (`MapGroup("/api/audit")`):
  - `GET /entries` (Admin policy: audit query endpoint with entity and actor filters)

---

## 8. Step-by-Step Task Checklist
- [x] Document updated architecture and database entity mapping.
- [ ] Implement `AuditLogEntry` entity and `RecordAuditEntry` message contract.
- [ ] Setup `AuditDbContext` with schema `audit` and JSONB mapping.
- [ ] Implement Wolverine handler for `RecordAuditEntry`.
- [ ] Add unit tests verifying factory invariants and immutability.
