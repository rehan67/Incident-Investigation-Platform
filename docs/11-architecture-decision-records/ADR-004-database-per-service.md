# ADR-004: Database-per-Service Pattern

## Status
Accepted

## Date
2026-07-01

## Context
In a microservices architecture, how services access and manage data is a fundamental decision. The simplest approach is a **Shared Database** where services read/write to the same logical database instance and schemas. While easy to build initially, shared databases create tight coupling, prevent services from scaling independently, and can cause cascading performance issues if one service locks shared tables.

We need to decide on the data storage boundary for the platform's 11 microservices.

## Decision
We will enforce the **Database-per-Service** pattern. 

Each microservice will own and manage its distinct logical database. Services are strictly forbidden from accessing another service's database directly. Any cross-boundary data queries or updates must occur through:
1.  Synchronous HTTP/REST API endpoints.
2.  Asynchronous Domain Events published to Kafka topics.

Each service can select the database engine best suited for its workload. For instance, the **Alarm History Service** will use **TimescaleDB** (time-series), while the **SOP Service** will use **PostgreSQL + Elasticsearch** (relational + full-text indexing).

## Alternatives Considered
*   **Shared Database (Single SQL Server/PostgreSQL Instance with shared tables)**: Rejected. Modifying a table in the Equipment database would require coordinated deployments of the Investigation and Report services. A lock on the alarm table during ingestion spikes would block the incident reporting UI.

## Consequences
### Benefits:
*   **Loose Coupling**: Schema changes in the `reports` database do not affect the `incidents` or `equipment` codebases. Developers can iterate and deploy services independently.
*   **Targeted Scaling**: The Alarm database (TimescaleDB) can scale compute and storage to handle billions of events, while the SOP database remains small and cost-effective.
*   **Polyglot Persistence**: Services choose databases optimized for their specific access patterns (time-series, relational, document, or search).
*   **Data Integrity**: Domain rules are encapsulated within the owning service's API bounds.

### Trade-offs:
*   **Distributed Transactions**: We cannot run simple SQL `JOIN` statements across tables or execute atomic multi-table commits. Transactions must be managed via the **Saga Pattern** and eventual consistency.
*   **Data Redundancy**: Some data (e.g., equipment asset tags) is cached or duplicated across services. Services must maintain this data using event-driven synchronization.
*   **Operational Complexity**: Requires configuring and backing up multiple logical databases (10 PostgreSQL schemas + 1 TimescaleDB).
