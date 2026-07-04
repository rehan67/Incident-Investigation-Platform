# 10 — Sequence Diagrams & Workflows

## 1. Happy Path Workflow Sequence

The diagram below details the successful end-to-end execution of a machine downtime report, automated data assembly, AI prediction, and final engineer review.

```mermaid
sequenceDiagram
    autonumber
    actor Engineer as 🧑‍🔧 Equipment Engineer
    participant React as 🖥️ React SPA (UI)
    participant APIM as 🚪 API Gateway (APIM)
    participant INC as 📋 Incident Service
    participant KAFKA as 📨 Apache Kafka
    participant ORCH as 🎯 Investigation Orchestrator
    participant DATA as 🏭 Data Services<br/>(Equip, Alarm, SOP, Prod)
    participant AIGW as 🤖 AI Gateway Service
    participant EXT_AI as 🧠 External AI (OpenAI)
    participant RPT as 📊 Report Service
    participant NOTIF as 📧 Notification Service

    %% Stage 1: Incident Reporting
    Engineer->>React: Report Downtime Incident
    React->>APIM: POST /api/v1/incidents (with Idempotency-Key)
    APIM->>APIM: Validate JWT & Rate Limit
    APIM->>INC: Forward Create Incident Command
    INC->>INC: Write 'incidents_db' (Status: REPORTED)
    INC->>KAFKA: Publish 'incident.created' event
    INC-->>APIM: Return 201 Created (Incident ID)
    APIM-->>React: Return 201 Created
    React-->>Engineer: Show Incident Reported (Spinner)

    %% Stage 2: Orchestration & Context Assembly
    KAFKA->>ORCH: Consume 'incident.created' event
    ORCH->>ORCH: Start Saga (Status: GATHERING_CONTEXT)
    
    par Parallel Data Gathering
        ORCH->>DATA: GET /api/v1/equipment/{id} (Profile + Maintenance)
        DATA-->>ORCH: Return Equipment JSON
    and
        ORCH->>DATA: GET /api/v1/alarms?eq={id}&window=10m (TimescaleDB)
        DATA-->>ORCH: Return Alarms JSON
    and
        ORCH->>DATA: GET /api/v1/sops/relevant?eq={id} (Elasticsearch)
        DATA-->>ORCH: Return SOPs JSON
    and
        ORCH->>DATA: GET /api/v1/production/runs/recent
        DATA-->>ORCH: Return Production Context JSON
    end

    ORCH->>ORCH: Assemble Context Package (Status: CONTEXT_READY)
    
    %% Stage 3: AI Inference & Validation
    ORCH->>AIGW: POST /internal/ai/analyze (Context Package)
    AIGW->>AIGW: Retrieve Prompt Template & Redact PII
    AIGW->>EXT_AI: Outbound HTTPS POST (Inference)
    EXT_AI-->>AIGW: Return Raw AI Completion
    AIGW->>AIGW: Validate JSON schema, confidence & safety
    AIGW-->>ORCH: Return Validated AnalysisResult
    ORCH->>ORCH: Save Results to DB (Status: COMPLETED)
    ORCH->>KAFKA: Publish 'investigation.completed' event

    %% Stage 4: Report Generation & Notification
    KAFKA->>RPT: Consume 'investigation.completed' event
    RPT->>RPT: Map findings & create Draft Report
    RPT->>RPT: Save 'reports_db' & upload PDF to Blob Storage
    RPT->>KAFKA: Publish 'report.generated' event
    
    KAFKA->>NOTIF: Consume 'report.generated' event
    NOTIF->>Engineer: Send email alert (Draft Report Link)
    React->>React: Update UI state (Report Ready)

    %% Stage 5: Engineer Audit & Final Submission
    Engineer->>React: Open Report Link
    React->>APIM: GET /api/v1/reports/{id}
    APIM->>RPT: Forward Query
    RPT-->>React: Return Draft Report Detail
    Engineer->>React: Modify findings or actions
    React->>APIM: PATCH /api/v1/reports/{id}
    APIM->>RPT: Forward Update
    RPT->>RPT: Write version 2 history
    RPT-->>React: Return 200 OK (Updated)
    
    Engineer->>React: Click Submit Report
    React->>APIM: PUT /api/v1/reports/{id}/submit
    APIM->>RPT: Forward Submit
    RPT->>RPT: Lock Report (Status: SUBMITTED)
    RPT->>KAFKA: Publish 'report.submitted' event
    RPT-->>React: Return 200 OK
    KAFKA->>INC: Consume 'report.submitted'
    INC->>INC: Close Incident (Status: CLOSED)
    React-->>Engineer: Show Report Submitted & Incident Closed
```

> [!TIP]
> **Visual Reference**: If the diagram above does not render in your markdown viewer, you can view the exported image file directly:
> ![Happy Path Workflow](sequence_diagram.png)

---

## 2. Error Recovery & Fallback Flows

Architecting for failure in manufacturing is mandatory. The sequence below demonstrates how the system degrades gracefully when downstream services or the AI endpoint experience outages.

### Circuit Breakers, Retries, and AI Failovers

```mermaid
sequenceDiagram
    autonumber
    participant ORCH as 🎯 Investigation Orchestrator
    participant DATA as 🏭 Data Service (Down)
    participant AIGW as 🤖 AI Gateway Service
    participant AI_PRIM as 🧠 Primary AI (Azure OpenAI)
    participant AI_SEC as 🧠 Secondary AI (AWS Bedrock)
    participant KAFKA as 📨 Apache Kafka

    %% Scenario A: Data Service Downtime (Circuit Breaker)
    Note over ORCH, DATA: Scenario A: Downstream Service Down
    ORCH->>DATA: GET /api/v1/equipment/123
    Note over ORCH: Call fails / times out
    ORCH->>DATA: GET /api/v1/equipment/123 (Retry 1 with backoff)
    Note over ORCH: Timeout (Circuit Breaker opens)
    ORCH->>ORCH: Fallback: Load equipment snapshot from Redis Cache
    Note over ORCH: Mark Context Status: PARTIAL_CONTEXT_WARNING

    %% Scenario B: AI Failure & Provider Switch
    Note over ORCH, AI_SEC: Scenario B: AI Inference Failure
    ORCH->>AIGW: POST /internal/ai/analyze (Context Package)
    AIGW->>AI_PRIM: HTTPS POST (Inference)
    AI_PRIM-->>AIGW: 503 Service Unavailable / Timeout
    AIGW->>AIGW: Retry primary endpoint (with backoff - fails)
    
    Note over AIGW: Fallback strategy triggered
    AIGW->>AI_SEC: HTTPS POST to Secondary Provider (Bedrock)
    AI_SEC-->>AIGW: Return Raw Completion JSON
    AIGW->>AIGW: Validate structure (Successful)
    AIGW-->>ORCH: Return Normalised AnalysisResult
    Note over ORCH: Investigation succeeds via backup provider
    ORCH->>KAFKA: Publish 'investigation.completed' (flagged with failover info)
```

### Scenario C: Complete AI Validation Failure (Manual Override Gate)
If both primary and secondary AI services fail to respond, or return data that fails validation checks (e.g., hallucinated SOPs that do not exist), the orchestrator triggers the manual investigation process:

```
[Both AI Providers Offline / Invalid Output]
                      │
                      ▼
[AI Gateway returns AI_VALIDATION_FAILED to Orchestrator]
                      │
                      ▼
[Orchestrator updates Saga state to: MANUAL_REVIEW]
                      │
                      ▼
[Orchestrator publishes 'investigation.failed' event]
                      │
                      ▼
[Report Service creates empty Draft Report template]
                      │
                      ▼
[Notification Service alerts Engineer: "AI investigation offline. Please complete report manually."]
```

---

## 3. Workflow State Transition Rules

### 3.1 Incident State Transition Constraints
An incident's state is strictly validated on any update command. The diagram below represents the allowable state transitions:

```mermaid
stateDiagram-v2
    [*] --> REPORTED: Incident reported by engineer
    REPORTED --> UNDER_INVESTIGATION: Saga triggers investigation
    UNDER_INVESTIGATION --> AWAITING_REVIEW: Report generated by service
    AWAITING_REVIEW --> CLOSED: Engineer submits finalized report
    
    UNDER_INVESTIGATION --> AWAITING_REVIEW: Manual Override triggered (AI Down)
    
    CLOSED --> [*]
```

### 3.2 Investigation Saga State Constraints

```mermaid
stateDiagram-v2
    [*] --> INITIATED
    INITIATED --> GATHERING_CONTEXT
    GATHERING_CONTEXT --> CONTEXT_READY: All services respond
    GATHERING_CONTEXT --> CONTEXT_PARTIAL: Timeout/Error on non-critical service
    
    CONTEXT_READY --> AI_PROCESSING
    CONTEXT_PARTIAL --> AI_PROCESSING
    
    AI_PROCESSING --> AI_COMPLETED: Valid AI analysis returned
    AI_PROCESSING --> AI_FAILED: Inference failed / validation failed
    
    AI_FAILED --> RETRYING: Retries left
    RETRYING --> AI_PROCESSING
    
    AI_FAILED --> MANUAL_REVIEW: Max retries exceeded
    AI_COMPLETED --> GENERATING_REPORT
    
    GENERATING_REPORT --> COMPLETED
    MANUAL_REVIEW --> COMPLETED: Engineer handles manual logging
    
    COMPLETED --> [*]
```

---

*Next: [07 — Deployment Architecture](../07-deployment-architecture/README.md)*
