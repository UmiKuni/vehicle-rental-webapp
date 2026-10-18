# Shared Kernel & Building Blocks — Implementation Plan

> **Module Directory:** `src/Shared/CarRental.BuildingBlocks`  
> **Target Project:** `CarRental.BuildingBlocks.csproj`  
> **Dedicated Git Branch:** `feat/shared-kernel`  
> **Phase:** 0 (Foundation — Prerequisite for all modules)  
> **References:** [be/docs/domain-model-entities-updated.md](../../../docs/domain-model-entities-updated.md) (Section 1 & 2)

---

## 1. Objectives & Scope
The Shared Kernel (`CarRental.BuildingBlocks`) contains common domain primitives, shared value objects, result patterns, and outbox/messaging contracts. It contains **no business domain logic**. All 13 modules depend on this library.

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from current development baseline
git checkout -b feat/shared-kernel

# Verify project builds
dotnet build src/Shared/CarRental.BuildingBlocks/CarRental.BuildingBlocks.csproj
```

---

## 3. Detailed Component Specifications

### 3.1 Domain Primitives (`Domain/`)
- [x] **`Entity<TId>`**:
  - Generic base class where `TId : notnull`.
  - Properties: `TId Id { get; protected set; }`.
  - Value-based equality overrides (`Equals`, `operator ==`, `operator !=`, `GetHashCode`).
- [x] **`IDomainEvent`**:
  - Interface: `Guid EventId { get; }`, `DateTimeOffset OccurredAt { get; }`.
- [x] **`AggregateRoot<TId>`**:
  - Inherits `Entity<TId>`.
  - `private readonly List<IDomainEvent> _domainEvents = new();`
  - `IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();`
  - Methods: `protected void Raise(IDomainEvent domainEvent)`, `public void ClearDomainEvents()`.
  - Concurrency token: `public uint Version { get; private set; }` (for PostgreSQL `xmin` mapping).
- [x] **`ValueObject`**:
  - Abstract base record or class supporting structural equality.

### 3.2 Results, Errors & Exceptions (`Results/`)
- [x] **`Error`**:
  - `public sealed record Error(string Code, string Message)`
  - Predefined static errors: `Error.None`, `Error.NullValue`.
- [x] **`Result` & `Result<T>`**:
  - `readonly record struct Result(bool IsSuccess, Error Error)`
  - `readonly record struct Result<T>(bool IsSuccess, T Value, Error Error)`
  - Static factory methods: `Result.Success()`, `Result.Failure(Error error)`, `Result.Success(value)`.
  - Implicit conversion operators from `T` and `Error`.
- [x] **`DomainException`**:
  - `public class DomainException(string code, string message) : Exception(message)`
  - Used for developer invariant violations / bug-level assertion failures.
- [x] **`Guard`**:
  - Static helpers: `AgainstNull`, `AgainstNullOrWhiteSpace`, `AgainstOutOfRange`, `Against(bool condition, string code, string message)`.

### 3.3 Shared Value Objects (`Domain/ValueObjects/`)
| Value Object | Properties | Validation & Invariants | EF Core Mapping Convention |
|---|---|---|---|
| **`Money`** | `long Amount`, `string Currency = "VND"` | `Amount >= 0`, Currency uppercase 3 chars. Methods: `Add`, `Subtract` (no negative result), `Multiply(decimal)`, `Percent(decimal)`. Prohibit cross-currency operations. | `OwnsOne` / `ComplexProperty` -> `*_amount` (bigint), `*_currency` (varchar(3)) |
| **`DateTimeRange`** | `DateTimeOffset Start`, `DateTimeOffset End` | `End > Start`. Methods: `Overlaps(other)`, `Contains(instant)`, `Duration`, `TotalDays` (ceiling round-up). | `OwnsOne` / `ComplexProperty` -> `*_start`, `*_end` (`timestamptz`) |
| **`GeoPoint`** | `double Latitude`, `double Longitude` | Lat in `[-90, 90]`, Lon in `[-180, 180]`. | PostGIS `geography(Point, 4326)` |
| **`Address`** | `string Line`, `string Ward`, `string District`, `string City`, `string Country = "VN"`, `GeoPoint? Location` | `Line` and `City` required. | `OwnsOne` / `ComplexProperty` |
| **`PhoneNumber`** | `string E164` | Validates international format; normalizes VN numbers (`0xxx` -> `+84xxx`). | `varchar(20)` with ValueConverter |
| **`Rating`** | `byte Value` | `Value` must be between `1` and `5`. | `smallint` |
| **`FileReference`** | `string StorageKey`, `string? Url`, `bool IsPrivate`, `string ContentType`, `long SizeBytes` | If `IsPrivate == true`, `Url` should be null or signed-only. | `OwnsOne` / `ComplexProperty` |

### 3.4 Messaging & Outbox Abstractions (`Messaging/`)
- [x] **`IIntegrationEvent`**: Marker interface extending `IDomainEvent` for events sent outside module boundary.
- [x] **`IEventOutbox` / Wolverine Integration**:
  - Configure Wolverine transaction middleware to automatically dispatch raised `DomainEvents` as integration events within the same PostgreSQL transaction.
- [x] **`IdempotentHandler<TEvent>`**: Base abstraction or guideline for consuming handlers with idempotency tracking.

---

## 4. Architecture Tests Setup (`tests/CarRental.ArchitectureTests`)
As part of Phase 0, define the guardrails in `tests/CarRental.ArchitectureTests`:
- [x] Test 1: Modules must not reference other modules' internal namespaces (only `*.Contracts` and `CarRental.BuildingBlocks`).
- [x] Test 2: Domain entities must not have `public` setters.
- [x] Test 3: Domain classes must not depend on EF Core or ASP.NET Core packages.
- [x] Test 4: Integration events in `*.Contracts` must be immutable records.

---

## 5. Execution Checklist
- [x] Create `Domain/Entity.cs`, `Domain/AggregateRoot.cs`, `Domain/IDomainEvent.cs`, `Domain/ValueObject.cs`.
- [x] Create `Results/Error.cs`, `Results/Result.cs`, `Results/DomainException.cs`, `Domain/Guard.cs`.
- [x] Create Value Objects: `Money.cs`, `DateTimeRange.cs`, `GeoPoint.cs`, `Address.cs`, `PhoneNumber.cs`, `Rating.cs`, `FileReference.cs`.
- [x] Add unit tests in a dedicated `CarRental.BuildingBlocks.Tests` project to cover:
  - `Money` arithmetic, currency mismatch, negative guard.
  - `DateTimeRange` overlaps, boundary calculations.
  - `PhoneNumber` normalization.
  - `Result` monadic operations.
- [x] Implement NetArchTest rules in `tests/CarRental.ArchitectureTests`.
- [x] Commit changes to `feat/shared-kernel` and create PR to baseline.
