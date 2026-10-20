# Search Module — Implementation Plan

> **Module Directory:** `src/Modules/Search`  
> **Projects:** `CarRental.Modules.Search.csproj`, `CarRental.Modules.Search.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-search`  
> **Phase:** 4 (Platform Intelligence & Read Projections — Parallel Stream 1)  
> **PostgreSQL Schema:** `search`  
> **References:** [be/docs/domain-model-entities-reworked.md](../../../docs/domain-model-entities-reworked.md) (Section 4.8 & 5)

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
| **Data Model** | Read model with Elasticsearch / in-memory sync | Pure PostgreSQL + PostGIS Read Model: `VehicleSearchDocument` (table `VehicleSearchDocuments`). |
| **Document Properties** | Deeply nested JSON features and dates | Flattened search projection: `VehicleId`, `Location` (PostGIS `Geometry`), `Brand`, `Model`, `Seats`, `Transmission`, `PriceFrom`, `AverageRating`, `IsPublished`. |
| **Out-of-Order Event Handling** | Unspecified | LWW (Last-Write-Wins) timestamp tracking: `LastFleetEventAt`, `LastPricingEventAt`, `LastRatingEventAt` to ensure eventual consistency. |
| **Visibility Rule** | Complex active calendar check | Query returns records where `IsPublished == true && PriceFrom != null` [ASSUMPTION]. |
| **Published Events** | 0 published events | **0 published events**. |
| **Table Naming** | snake_case tables | Retained snake_case tables (`vehicle_search_documents`) matching PostgreSQL conventions. |

---

## 1. Objectives & Scope
The Search module provides high-performance, geo-spatial vehicle discovery (< 2s response time per NFR-01) backed by PostGIS spatial indexing (`ST_DWithin`). It maintains an eventually consistent read model projected from Fleet, Pricing, and Reviews integration events.

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after Phase 1-3 contracts are available
git checkout -b feat/module-search

# Build module
dotnet build src/Modules/Search/CarRental.Modules.Search/CarRental.Modules.Search.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks` (GeoPoint, Result, Guard).
- **Consumes Events from:**
  - `Fleet`: `VehiclePublished`, `VehicleUnpublished`
  - `Pricing`: `PricingUpdated`
  - `Reviews`: `RatingSummaryUpdated`

---

## 4. Contracts Project (`CarRental.Modules.Search.Contracts`)

### 4.1 Synchronous Interface
*(No synchronous interface required; read model queries are accessed via HTTP GET).*

### 4.2 Published Integration Events
- **0 Published Events** (Search is a pure read projection).

---

## 5. Domain Layer (`Entities/`, `ReadModels/`)

### 5.1 Read Model: `VehicleSearchDocument`
- Table `VehicleSearchDocuments`:
  - `Guid VehicleId` (Primary Key)
  - `GeoPoint Location` (PostGIS `Geometry`, SRID 4326)
  - `string Brand`
  - `string Model`
  - `int Seats`
  - `string Transmission`
  - `decimal? PriceFrom` (min rate from pricing)
  - `decimal? AverageRating`
  - `bool IsPublished`
  - `DateTimeOffset? LastFleetEventAt`
  - `DateTimeOffset? LastPricingEventAt`
  - `DateTimeOffset? LastRatingEventAt`
- Read Model Behavior (Idempotent Apply Methods):
  ```csharp
  void ApplyPublished(VehiclePublished e);
  void ApplyUnpublished(VehicleUnpublished e);
  void ApplyPrice(PricingUpdated e);
  void ApplyRating(RatingSummaryUpdated e);
  ```

---

## 6. Persistence & EF Core (`Data/`)
- `SearchDbContext`: `HasDefaultSchema("search")`.
- Table mapping (snake_case):
  - `vehicle_search_documents` (PK `vehicle_id`)
- PostGIS spatial indexing (`GIST` on `Location`).

---

## 7. Event Handlers & Endpoints (`EventHandlers/`, `Endpoints/`)
- Integration Event Handlers (Idempotent):
  - `VehiclePublishedHandler`
  - `VehicleUnpublishedHandler`
  - `PricingUpdatedHandler`
  - `RatingSummaryUpdatedHandler`
- Minimal API Endpoints (`MapGroup("/api/search")`):
  - `GET /vehicles` (query parameters: `lat`, `lon`, `radiusKm`, `brand`, `model`, `seats`, `transmission`, `minPrice`, `maxPrice`, `page`, `pageSize`)

---

## 8. Step-by-Step Task Checklist
- [x] Document updated architecture and database entity mapping.
- [ ] Implement `VehicleSearchDocument` read model and idempotent mutation methods.
- [ ] Setup `SearchDbContext` with schema `search` and PostGIS GIST indexing.
- [ ] Implement Wolverine integration event handlers for Fleet, Pricing, and Reviews events.
- [ ] Add unit and query tests for PostGIS radius calculation and projection idempotency.
