# ADR-002: Orchestration vs. Choreography for Investigation Workflow

## Status
Accepted

## Date
2026-07-01

## Context
The incident investigation workflow involves multiple steps:
1.  A downtime incident is reported.
2.  Data is collected from four distinct domains (Equipment, Maintenance, Alarms, SOPs, Production).
3.  The context is consolidated and analyzed via an external AI engine.
4.  A structured report is drafted and stored.
5.  Engineers are alerted.

In a distributed microservice environment, we must coordinate these steps while maintaining loose coupling. We have to choose between two patterns for Saga coordination:
*   **Choreographed Saga**: Services react to events autonomously without a central director.
*   **Orchestrated Saga**: A centralized service (Orchestrator) coordinates the workflow steps and drives the state machine.

## Decision
We will use an **Orchestrated Saga Pattern** managed by a dedicated service: the **Investigation Orchestrator**. 

The orchestrator will act as the single director of the investigation lifecycle. It consumes the `incident.created` event, queries downstream domain services to assemble the context package, coordinates the AI Gateway inference, triggers report generation, and updates the timeline.

## Alternatives Considered
*   **Choreographed Saga**: Rejected. In choreography, the Equipment Service would consume `incident.created` and publish `equipment.gathered`. The Alarm Service would consume that, run its query, and publish `alarms.gathered`. This creates a cyclic, highly-coupled dependency graph that is hard to maintain, trace, or modify.

## Consequences
### Benefits:
*   **Observable State**: The orchestrator tracks state transitions in the `investigation_steps` table. The frontend client can query a single API to show the exact status of the investigation pipeline (e.g., "Step 3/4: AI Analysis running").
*   **Centralised Error Recovery**: If the AI Gateway returns a validation failure, the orchestrator handles retry policies, fallbacks to backup providers, or routes the incident to a manual review state.
*   **Simplified Domain Services**: Downstream domain services (Equipment, Alarm, SOP) remain dumb data providers. They do not need to know about the "investigation workflow" lifecycle.
*   **Extensibility**: Adding a step to the workflow (e.g., "Human Approval before AI Dispatch" or "Submit to Safety Committee") only requires updating the orchestrator's state transitions, not modifying other services.

### Trade-offs:
*   **Single Point of Failure**: The orchestrator is a critical runtime component. If it crashes, automated investigations freeze. We mitigate this by running multiple pod replicas in AKS with zone-redundancy.
*   **Potential Hub-and-Spoke Coupling**: We must avoid turning the orchestrator into a monolith that contains domain business logic. It must only coordinate workflow state, delegating actual data processing to individual microservices.
