# Architecture Analysis: Car Rental Platform

Oct 11, 2026 · @Tuan

A modular monolith is the best fit for this project. It gives strong module boundaries and safe, transactional booking logic in a single deployable that a four-person team can build and run. This document analyses why, how well the design meets the key requirements, and what it costs.

## 1. Context and scope

The platform is a web application where customers rent cars from individual owners and companies. Admins approve vehicles, moderate reviews and resolve disputes. The API is built with .NET, and the scope of this analysis is the backend architecture: its structure, data ownership, module communication and quality attributes. The detailed module and entity list is in the Modular Monolith Architecture document.

## 2. Requirements that drive the architecture

Not every requirement shapes the architecture. These are the ones that force structural decisions.

| Driver | Why it matters architecturally |
| --- | --- |
| No double booking (BR-05, BR-06, NFR-08) | Two customers must never hold the same car for overlapping dates, even under concurrent requests |
| Price and policy lock (BR-08) | Later price changes must not alter existing orders |
| Payment integrity (BR-10) | Gateway timeouts must never leave a half-finished booking state |
| Fast search (NFR-01) | Search must stay quick without heavy multi-table joins |
| Reporting without load (NFR-02) | Admin statistics must not slow down transactions |
| Protected ID documents (NFR-05) | Identity files must be reachable only by authorised people |
| Tamper-proof handover evidence (BR-13, BR-14) | Photos and vehicle condition must be trustworthy, including when captured offline |
| Auditability (BR-17, NFR-13) | Admin actions and price changes must leave a permanent trail |
| Availability (NFR-03) | The system targets 99.5% uptime |
| Project constraints | A team of four, a fixed semester, .NET for the API |

## 3. Architecture options considered

Three styles were compared against the project's constraints. The ratings are the team's own qualitative assessment.

| Criterion | Layered monolith | Modular monolith | Microservices |
| --- | --- | --- | --- |
| Fit for a four-person team | Good | Good | Poor: many services to build and operate |
| Deployment and operations | One unit, simple | One unit, simple | Many units, needs orchestration and monitoring |
| Module isolation | Weak, tends to tangle over time | Strong if enforced with contracts, schema per module and architecture tests | Strongest, enforced by process boundaries |
| Booking consistency | Easy, one transaction | Easy, same database and exclusion constraint | Hard, needs sagas or distributed transactions |
| Independent scaling | None | Limited, scale the whole app | Per service |
| Cost to build | Lowest | Low to medium | High |
| Path to split later | Hard | Moderate, boundaries already exist | Not applicable |

## 4. Decision: modular monolith

The modular monolith was chosen because it keeps the consistency benefits of one database and one deployment, while still forcing the domain into clear modules. The deciding factors were:

- **Consistency where it matters.** Double-booking protection is a database constraint, not a distributed protocol.
- **Team size.** One deployable, one pipeline and one database keep operational work small.
- **Enforced boundaries.** Each module exposes only a contracts project, owns its own schema and shares no tables. Architecture tests (NetArchTest) fail the build if a module reaches into another.
- **An exit path.** Because modules already talk through contracts and events, a module can be extracted later without redesign.

## 5. Architecture overview

The system is one ASP.NET Core application behind a thin API host. The host handles authentication, routing and validation, and contains no business logic. Thirteen modules sit behind it, with PostgreSQL (one schema per module), Cloudinary for images and an in-process event bus with an outbox underneath.

| Group | Modules | Role |
| --- | --- | --- |
| Rental flow (core domain) | Booking, Pricing, Availability, Payments, Handover | The rental lifecycle from quote to return |
| Catalog and people | Users, Fleet, Search, Reviews | Accounts, vehicles, discovery and ratings |
| Supporting and platform | Support, Notifications, Audit, Reporting | Disputes, messaging, audit trail and statistics |

### Communication rules

- **Contracts only.** Modules reference each other's contracts project and nothing else.
- **No cross-schema joins or foreign keys.** Modules store only the other module's IDs.
- **Synchronous calls** for answers needed immediately: Booking to Pricing (quote), Availability (hold) and Users (profile verified).
- **Events through an outbox** for everything else, such as `BookingConfirmed` or `PaymentSucceeded`. These feed Notifications, Search, Reporting, Audit and Reviews.
- **Admin is not a module.** Admin use cases are endpoints inside the owning module, protected by an `Admin` policy.

### Inside each module

Each module uses a simple layered structure instead of Clean Architecture, which is easier for a four-person team to manage.

- **Layers.** Endpoints, Services, Entities and Data in one project per module, plus a separate public Contracts project.
- **Dependencies point downward.** Endpoints call Services, and Services use Entities and the module's `DbContext`.
- **Internals stay `internal`.** Only the Contracts project is public, so other modules cannot reach into services or entities.
- **No entity leaves its module.** Entities are mapped to Contracts DTOs.
- **Rules live in the entities** for Booking, Availability and Payments (for example `booking.Accept()`), so the status machine is not scattered across long service methods.
- **Tests use a real database** (Testcontainers) instead of mocked repositories.


Status machine: Pending, Confirmed, Paid, InProgress, Completed, with Rejected, Expired and Cancelled as exits.

## 6. Quality scenarios: how the design meets the requirements

Each row is a concrete situation, the design decision that handles it, and an honest assessment.

| Requirement | Scenario | Design decision | Assessment |
| --- | --- | --- | --- |
| No double booking | Two customers request overlapping dates at the same moment | Availability hold plus a PostgreSQL exclusion constraint on `tstzrange` per vehicle | Met. Prove it with a concurrent-request test on a real database |
| Price lock | The owner raises the price after a booking exists | Booking stores a price and cancellation-policy snapshot | Met |
| Payment integrity | The gateway times out during payment | Payment stays Pending and Booking is unchanged; only a signed webhook moves it to Paid; a webhook inbox makes processing idempotent | Met |
| Fast search | A guest searches by location and dates | Search read model in a denormalized table with PostGIS; availability is checked only for the top results | Likely met. Needs a load test against the target in the requirements |
| Reporting without load | An admin opens statistics at peak time | Reporting reads its own snapshots built from events | Met |
| Protected ID documents | Someone tries to open another user's ID file | Cloudinary with authenticated delivery for ID documents; signed URLs issued only to the booking's owner and admins | Met |
| Trustworthy handover evidence | A phone captures photos offline | The server stamps `ServerReceivedAt`; the phone's `ClientCapturedAt` is untrusted metadata; rows are read-only after both parties confirm | Met |
| Auditability | An admin overrides a booking | The override goes through the owning module and writes an append-only audit entry; price history is versioned | Met |
| Availability (99.5%) | The server or database restarts | One deployable with health checks and automated restart; backups on the database | Partly met. The single database is a single point of failure |

## 7. Trade-offs and risks

| Risk or trade-off | Impact | Mitigation |
| --- | --- | --- |
| Pending requests hold the calendar for up to 12 hours | One customer can briefly block a car without paying | Short hold expiry; alternatively allow overlapping requests and re-check on the owner's accept |
| Module boundaries erode under deadline pressure | The monolith turns into a tangled one | Architecture tests in CI and code review on cross-module references |
| Eventual consistency between modules | Search or reports can lag a few seconds behind bookings | Accept the delay for read models; keep booking-critical checks synchronous |
| Single database and single deployable | One failure affects everything; cannot scale one module alone | Backups, health checks, caching; extract hot modules later if needed |
| Thirteen modules for four developers | High overhead early on | Start with Pricing and Availability inside Fleet, and Notifications, Audit and Reporting as one Platform module, keeping namespaces separate |
| Payment gateway dependency | Outages or sandbox limits stop deposits | Isolate the gateway behind a Payments interface; use sandbox and a manual receipt path |
| Open design question: order of owner approval and payment | The project document and the use cases disagree | This design follows the use cases (owner approves, then customer pays); only the Booking state machine changes if the team reverses it |

## 8. Evolution path

If the system grows, the modules most ready to become separate services are:

- **Search.** It is already a read model fed by events.
- **Notifications.** It already only consumes events.
- **Payments.** Its gateway integration is already isolated.

Extraction needs two steps: replace the in-process bus with a message broker, and move the module's schema to its own database. Contracts, events and schema ownership are already in place, so no module has to be rewritten.

## 9. Technology summary

| Layer | Choice |
| --- | --- |
| Runtime and API | .NET 9, ASP.NET, one endpoint group per module |
| Data | PostgreSQL with PostGIS, EF Core with one `DbContext` per module |
| Messaging and outbox | Wolverine |
| Authentication | ASP.NET Core Identity with JWT and refresh tokens, Google and Facebook sign-in |
| Files | Cloudinary for images; ID documents (CCCD, GPLX) are uploaded with authenticated delivery and reached only through signed URLs |
| Cache and real-time | Redis, SignalR |
| Quality | xUnit, Testcontainers, NetArchTest, Serilog, OpenTelemetry |

## 10. Conclusion

The modular monolith meets every architecturally significant requirement, with uptime only partly met because of the single database. Its main costs are discipline to keep boundaries clean and a short delay between modules for read models. For a four-person team under a semester deadline, it delivers the safety of a monolith with a clear route to services if the platform grows.
