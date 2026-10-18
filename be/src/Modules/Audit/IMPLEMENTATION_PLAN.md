# Audit Module — Implementation Plan

> **Module Directory:** `src/Modules/Audit`  
> **Projects:** `CarRental.Modules.Audit.csproj`, `CarRental.Modules.Audit.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-audit`  
> **Phase:** 4 (Projections & Platform — Parallel Stream 3)  
> **PostgreSQL Schema:** `audit`  
> **References:** [be/docs/domain-model-entities-updated.md](../../../docs/domain-model-entities-updated.md) (Section 4.12 & 5)

---

## 1. Objectives & Scope
The Audit module provides an immutable, append-only security log of all sensitive domain actions, administrative overrides, and private document accesses. It ensures non-repudiation, tamper-resistance, and regulatory compliance.

**Key Architecture Decision:** Audit does **not** subscribe to business integration events. Instead, `Audit.Contracts` exposes a single command message, `RecordAuditEntry`, which is dispatched through the transactional outbox by an Application-layer pipeline behavior of each module for:
1. Every command implementing `IAuditedCommand` (admin actions, role/status changes, approvals/rejections).
2. Every access to a sensitive document (issuance of signed URLs for CCCD, Driver License, Payment Receipts). This captures critical security actions that raise no business domain event.

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after Phase 1 & 2 contracts are available
git checkout -b feat/module-audit

# Build module
dotnet build src/Modules/Audit/CarRental.Modules.Audit/CarRental.Modules.Audit.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks` (Result, Guard, Entity).
- **Inbound Message Flow:**
  - Receives `RecordAuditEntry` messages sent via Wolverine Outbox from Application-layer pipeline behaviors across all modules.
  - Zero subscriptions to business integration events (`Booking*`, `Payment*`, etc.).

---

## 4. Contracts Project (`CarRental.Modules.Audit.Contracts`)
### 4.1 Audit Command / Message (`RecordAuditEntry`)
```csharp
public sealed record RecordAuditEntry(
    DateTimeOffset OccurredAt,
    Guid? ActorId,
    string ActorType,
    string SourceModule,
    string Action,
    string EntityType,
    string EntityId,
    string? ChangesJson,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId
);
```

### 4.2 Marker Interfaces
```csharp
/// <summary>
/// Marker interface placed on commands that require automated audit trail recording.
/// </summary>
public interface IAuditedCommand
{
    string Action { get; }
    string EntityType { get; }
}
```

### 4.3 Synchronous Interface (`IAuditApi`)
```csharp
public interface IAuditApi
{
    Task RecordAsync(RecordAuditEntry record, CancellationToken ct = default);
}
```

---

## 5. Domain Layer (`Entities/`, `Enums/`)

### 5.1 Enums
- `ActorType` (`User`, `Admin`, `System`)

### 5.2 Aggregate Root: `AuditEntry` (Append-Only)
- Properties:
  - `Guid Id`
  - `DateTimeOffset OccurredAt`
  - `Guid? ActorId`
  - `ActorType ActorType`
  - `string SourceModule`
  - `string Action` (e.g., `identity.approved`, `vehicle.rejected`, `document.viewed`, `payment.refunded`)
  - `string EntityType`
  - `string EntityId`
  - `string? ChangesJson`
  - `string? IpAddress`, `string? UserAgent`, `string? CorrelationId`
- Invariants:
  - Static factory `Record(...)` only.
  - Strictly **no update or delete methods**.
  - Enforces data masking: Sensitive personal identification numbers (CCCD/GPLX) must never be stored in `ChangesJson`.

---

## 6. Persistence & EF Core (`Data/`)
- `AuditDbContext`: `HasDefaultSchema("audit")`.
- Repository Interface:
  ```csharp
  public interface IAuditRepository
  {
      Task AddAsync(AuditEntry entry, CancellationToken ct = default);
      Task<PagedResult<AuditEntry>> QueryAsync(AuditQuery query, CancellationToken ct = default);
  }
  ```
- Database Permissions & Immutability:
  - Migration configures PostgreSQL table with permissions restricting `UPDATE` and `DELETE`:
    ```sql
    REVOKE UPDATE, DELETE ON TABLE audit.audit_entries FROM carrental_app_role;
    ```
- Migration: `AddAuditInitialMigration`.

---

## 7. Handlers & Endpoints (`Handlers/`, `Endpoints/`)
- Wolverine Audit Handler:
  ```csharp
  public class RecordAuditEntryHandler
  {
      private readonly IAuditRepository _repository;
      public RecordAuditEntryHandler(IAuditRepository repository) => _repository = repository;

      public async Task Handle(RecordAuditEntry message, CancellationToken ct)
      {
          var entry = AuditEntry.Record(
              message.OccurredAt,
              message.ActorId,
              Enum.Parse<ActorType>(message.ActorType),
              message.SourceModule,
              message.Action,
              message.EntityType,
              message.EntityId,
              message.ChangesJson,
              message.IpAddress,
              message.UserAgent,
              message.CorrelationId);

          await _repository.AddAsync(entry, ct);
      }
  }
  ```
- Admin Endpoints (`RequireAuthorization("Admin")`):
  - `GET /api/admin/audit-logs` (Filtered by entity, actor, date range, action).

---

## 8. Step-by-Step Task Checklist
- [ ] Implement `ActorType` and immutable `AuditEntry` aggregate root.
- [ ] Define `RecordAuditEntry` message and `IAuditedCommand` marker in `CarRental.Modules.Audit.Contracts`.
- [ ] Implement data sanitizer to mask PII in `ChangesJson` before dispatch.
- [ ] Setup `AuditDbContext` with append-only repository.
- [ ] Write EF Core migration with PostgreSQL revocation on UPDATE/DELETE (`REVOKE UPDATE, DELETE`).
- [ ] Implement Wolverine handler for `RecordAuditEntry`.
- [ ] Build Admin audit query Minimal API endpoints.
- [ ] Unit tests:
  - `Sanitizer_MasksIdentityDocumentNumbersCorrectly`
  - `AuditEntry_HasNoPublicMutators`
- [ ] Integration tests verifying append-only behavior with Testcontainers PostgreSQL.

