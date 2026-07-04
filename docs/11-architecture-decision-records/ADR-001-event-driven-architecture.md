# ADR-001: Event-Driven Communication Backbone

## Status
Accepted

## Date
2026-07-01

## Context
Microservice communication requires coordination during incident reporting and subsequent investigation pipelines. Synchronous HTTP/REST calls are easy to implement but lead to tightly-coupled services, block system threads during long-running tasks, and can cause cascading failures if a single downstream service fails. 

Furthermore, semiconductor manufacturing operates 24/7. Telemetry data (alarms, maintenance status changes) occurs at a high rate and needs to be ingested reliably without impacting core transaction response times. We need a durable communication mechanism that decouples services and facilitates future data science pipelines.

## Decision
We will adopt an **Asynchronous Event-Driven Architecture (EDA)** as the primary communication style for state changes and lifecycle notifications, powered by **Apache Kafka** (via Azure Event Hubs). 

Synchronous HTTP/REST calls will be limited to:
*   Inbound client queries (read-only requests to API Gateway).
*   Context collection requests during the Orchestrator's gathering phase (where immediate response is required).
*   Direct commands that require instantaneous blocking responses.

All service-to-service state updates (e.g., `incident.created`, `report.generated`, `report.submitted`) will be published as structured events to Kafka topics.

## Alternatives Considered
*   **Synchronous REST-Only Communication**: Rejected. High coupling. If the Report Service goes down, the Incident Service would fail to register incident submissions. This would increase downtime tracking errors.
*   **RabbitMQ / AMQP Message Queues**: Rejected. RabbitMQ is excellent for simple task distribution, but it lacks the persistent log and replay capabilities of Apache Kafka. Replaying historical incident events is vital for training future AI models on past engineer edits.

## Consequences
### Benefits:
*   **Temporal Decoupling**: If the Notification Service is offline during a report generation event, it will process the email/MS Teams alerts immediately upon recovery without dropping data.
*   **Extensibility for AI**: Future ML and Agentic models can act as new event consumers on the `incidents` or `reports` topics without modifying existing service code.
*   **Durable Audit Trail**: Events are persisted in Kafka, forming a natural, immutable ledger of all operational state changes.
*   **Replayability**: We can replay historical event streams to debug race conditions, re-run failed sagas, or train offline AI models.

### Trade-offs:
*   **Event Consistency**: We adopt an Eventual Consistency model instead of strong transactions across boundaries. Distributed reads might serve slightly stale data during active writes.
*   **Operational Overhead**: Requires managing a Kafka partition strategy and consumer group offsets.
*   **Distributed Debugging**: Tracking issues requires structured logging with distributed tracing (`Correlation-Id`) across service boundaries.
