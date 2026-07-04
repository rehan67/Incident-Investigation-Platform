# ADR-005: Core Technology Stack Selection

## Status
Accepted

## Date
2026-07-01

## Context
Choosing the runtime languages, framework stacks, database engines, and message brokers determines the platform's stability, developer velocity, deployment efficiency, and alignment with corporate IT standards. 

The manufacturing organization requires an enterprise-grade platform that integrates cleanly with Azure cloud resources and runs reliably on Kubernetes.

## Decision
We select the following core technologies:

*   **Backend Services**: **.NET 10 (C#)**
    *   Selected for high performance (Kestrel web server), strong static typing, excellent Azure SDK integration, and native compilation capability (Native AOT) to minimize Kubernetes pod startup times.
*   **Frontend UI**: **React with TypeScript**
    *   Selected for component-based modularity, large ecosystem, and familiarity among development teams.
*   **Message Broker**: **Apache Kafka (Azure Event Hubs)**
    *   Selected for durable event streaming, partitioning scalability, and the ability to replay historical event streams (critical for auditing and offline machine learning model training).
*   **Databases**: **PostgreSQL** (relational) + **TimescaleDB** (time-series)
    *   PostgreSQL provides ACID transactional features, JSONB columns, and a cost-effective managed offering. TimescaleDB partitions high-frequency alarm data efficiently in the same SQL dialect.

## Alternatives Considered
*   **Java / Spring Boot (Backend)**: Considered. Spring Boot is extremely mature, but C#/.NET 10 exhibits lower memory overhead per pod and better integration with Azure services like App Configuration and Workload Identity.
*   **Node.js / Express (Backend)**: Rejected. Lacks strong CPU concurrency for large context aggregation tasks. TypeScript provides compiler typing but doesn't solve runtime type-safety requirements.
*   **RabbitMQ (Messaging)**: Rejected. RabbitMQ does not persist events post-consumption. We need event replayability to train future AI agents.

## Consequences
### Benefits:
*   **Performance**: .NET 10 has near-native performance, low memory footprint, and quick startup latency, reducing AKS hosting costs.
*   **Type Safety**: End-to-end type safety (TypeScript on frontend, C# on backend) reduces runtime null-reference exceptions.
*   **Ecosystem Maturity**: Clean separation of concerns using EF Core (data access), MediatR (CQRS), and Polly (resilience).
*   **Portability**: Writing to standard PostgreSQL and Kafka standards prevents strict cloud vendor lock-in.

### Trade-offs:
*   **Learning Curve**: .NET 10 requires developers to have strong C# object-oriented design and async processing skills.
*   **JSON Handling Overhead**: Mapping JSONB columns in PostgreSQL to C# objects requires careful configuration of System.Text.Json options within EF Core.
