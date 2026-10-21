-- ==============================================================================
-- Car Rental Platform: PostgreSQL Database Initialization Script
-- Executed automatically on container creation by PostgreSQL entrypoint.
-- ==============================================================================

-- 1. Required PostgreSQL Extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "postgis";
CREATE EXTENSION IF NOT EXISTS "btree_gist";

-- 2. Module Schemas (Schema-per-Module Modular Monolith Boundary)
CREATE SCHEMA IF NOT EXISTS "users";
CREATE SCHEMA IF NOT EXISTS "fleet";
CREATE SCHEMA IF NOT EXISTS "pricing";
CREATE SCHEMA IF NOT EXISTS "availability";
CREATE SCHEMA IF NOT EXISTS "booking";
CREATE SCHEMA IF NOT EXISTS "payments";
CREATE SCHEMA IF NOT EXISTS "handover";
CREATE SCHEMA IF NOT EXISTS "reviews";
CREATE SCHEMA IF NOT EXISTS "support";
CREATE SCHEMA IF NOT EXISTS "search";
CREATE SCHEMA IF NOT EXISTS "notifications";
CREATE SCHEMA IF NOT EXISTS "audit";
CREATE SCHEMA IF NOT EXISTS "reporting";

-- 3. Wolverine Message & Outbox Storage Schema
CREATE SCHEMA IF NOT EXISTS "wolverine_queues";
