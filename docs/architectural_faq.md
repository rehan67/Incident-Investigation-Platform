# Architectural FAQ

This document addresses common architectural questions, design trade-offs, and critical decisions made for the **Incident Investigation Platform**.

---

### Q1: Why did you choose an Orchestrated Saga over a Choreographed Saga? (ADR-002)
*   **Context**: Choreography (event-chaining) is often preferred in simple microservice architectures because it has no central coordinator.
*   **Answer**: 
    The incident investigation workflow has a strict, sequential dependency chain: **Gather Context $\rightarrow$ Validate $\rightarrow$ AI Inference $\rightarrow$ Report Drafting $\rightarrow$ Notify**. 
    
    If we used choreography, we would have a "spaghetti flow" of events where tracking the state of an investigation is extremely difficult. By introducing the **Investigation Orchestrator** as the coordinator, we gain:
    1.  **State Visibility**: A single, queryable source of truth for the active state of any investigation saga.
    2.  **Centralized Resilience**: A single place to manage timeouts, retries, and manual overrides if a data source fails.
    3.  **Extensibility**: Adding new analysis steps or introducing human-in-the-loop gates in the future only requires updating the Orchestrator saga definition rather than re-triggering events across multiple separate services.

---

### Q2: How does the platform handle Kafka outages? Is it a single point of failure? (ADR-001)
*   **Context**: Event-driven systems rely heavily on the message broker. If the broker crashes, communication stops.
*   **Answer**:
    We decoupled inbound client requests from the event backbone to prevent outages from blocking engineers:
    1.  **Write-Local First**: When an engineer reports downtime, the Incident Service writes to its local `incidents_db` database **first**. If Kafka is offline, the client still receives a `201 Created` HTTP response, and the incident is saved.
    2.  **Transactional Outbox Pattern**: A background worker polls the database for unpublished incidents and retries publishing them to Kafka once the broker is back online.
    3.  **High Availability**: We use **Azure Event Hubs** (which exposes a Kafka-compatible endpoint) configured with multi-zone replication, guaranteeing $99.99\%$ availability at the managed cloud tier.

---

### Q3: Why use a Database-per-Service pattern instead of a single Shared Database?
*   **Context**: A shared database allows faster joins and lowers infrastructure costs.
*   **Answer**:
    Shared databases are a major microservices anti-pattern because they lead to tight schema-level coupling (e.g., a table change in the equipment service breaks the report service). Database-per-Service gives us:
    1.  **Independent Deployability**: Teams can deploy updates and alter schemas without coordinating across all 11 services.
    2.  **Polyglot Persistence**: Different services have different data needs. We use standard **PostgreSQL** for transactional entities (incidents, reports), a **TimescaleDB hypertable** for high-volume time-series alarms, and **Elasticsearch** for fast full-text search and vector embeddings of SOPs.
    3.  **No Shared State**: Data is aggregated cleanly in the application tier (the Orchestrator querying service endpoints in parallel via REST).

---

### Q4: AI is non-deterministic and can hallucinate. How do you ensure safety in a manufacturing environment? (Doc 08 - AI Strategy)
*   **Context**: If the AI recommends a dangerous action (e.g., "bypass safety interlock"), it could damage million-dollar equipment or harm personnel.
*   **Answer**:
    We apply a **Five-Layer Validation Pipeline** inside the **AI Gateway Service** before any payload is accepted:
    1.  **JSON Schema Validation**: Confirms the response matches the expected JSON format.
    2.  **Completeness Gate**: Verifies required fields (like `rootCause` and `correctiveActions`) are populated.
    3.  **Confidence Score Gate**: Checks the AI's self-assessed confidence. If it is $< 0.70$, the report is flagged with a `LOW_CONFIDENCE` warning.
    4.  **SOP Reference Match**: Verifies suggested SOP IDs against our Elasticsearch database. If the AI suggests a non-existent SOP (hallucination), it is immediately stripped.
    5.  **Safety Guard (RegEx & Keyword Scan)**: Sweeps the text for safety-critical terms. If a dangerous action is suggested, the gateway marks the report as `SAFETY_REVIEW_REQUIRED`, locking the draft and forcing a manual sign-off by a Lead Engineer before it can be finalized.

---

### Q5: Why did you choose AKS over Azure Container Apps (ACA)? (ADR-007)
*   **Context**: Azure Container Apps is serverless and easier to manage than standard Kubernetes.
*   **Answer**:
    While ACA simplifies deployments, AKS was chosen to meet the strict regulatory, portability, and security standards of semiconductor manufacturing:
    1.  **Hybrid/On-Premise Portability**: Fabs (fabrication plants) often operate on-premise for latency and IP security. AKS uses standard Helm charts and Kubernetes manifests, allowing the platform to run on-premise (e.g., on Rancher or MicroK8s) or in any cloud with zero configuration changes.
    2.  **Zero-Trust Networking**: AKS supports **Azure CNI** and **NetworkPolicies (Calico)**. This allows us to enforce strict network-level firewalls between pods (e.g., preventing the Auth pod from speaking to the Report DB).
    3.  **GitOps Drift Resolution**: AKS allows running standard Kubernetes Operators in-cluster. We run the **FluxCD Operator** inside the cluster to automatically sync configurations from Git and resolve configuration drift.
    4.  **Key Vault CSI Driver**: AKS mounts Key Vault secrets directly into the container's memory filesystem as a temporary volume, preventing credentials from persisting as plain-text environment variables (standard in ACA).

---

### Q6: Why use both OpenTelemetry/Grafana and Azure Monitor? (Doc 09 - Non-Functional)
*   **Context**: Why maintain two monitoring stacks instead of consolidating?
*   **Answer**:
    They monitor two completely different tiers of the system:
    1.  **Azure Monitor (Platform Logs)**: Monitors **cloud infrastructure PaaS** that does not run our C# code (e.g., Virtual Network flow logs, Azure Firewall blocks, Key Vault access audits, and APIM request rates). These logs cannot run OpenTelemetry sidecars and must flow to Log Analytics.
    2.  **OpenTelemetry + Grafana (App Logs/Metrics/Traces)**: Monitors **our microservice code**. It collects distributed traces (W3C traceparent headers propagating across Kafka) and service metrics (like Saga completion times), pushing them to Prometheus, Loki, and Grafana.
    3.  **Single Pane of Glass**: We use Grafana's native **Azure Monitor plugin** to pull infrastructure metrics directly into our application dashboards, giving engineers a single dashboard for both systems.

---

### Q7: Why did you decide NOT to adopt a Service Mesh (e.g., Istio)? (ADR-006)
*   **Context**: Large microservice setups use service meshes for traffic routing, mTLS, and observability.
*   **Answer**:
    For a platform with 11 microservices, a Service Mesh adds unnecessary operational complexity, heavy CPU/RAM sidecar resource overhead, and increases learning curves. K8s native services handle DNS routing perfectly. We implement code-level HTTP resilience (circuit breakers, retries) natively using Polly and secure network boundaries via standard NetworkPolicies, making a Service Mesh overhead without return-on-investment at our scale.

---

### Q8: How is data consistency maintained across these isolated microservice databases?
*   **Context**: A database-per-service pattern makes distributed transactions (like 2-Phase Commit) slow and hard to scale.
*   **Answer**:
    We enforce **Eventual Consistency** using Kafka event propagation and the Saga Orchestrator:
    1.  When a state change happens in one domain (e.g., an incident is created), the service commits locally and publishes a durable event.
    2.  Downstream services consume this event asynchronously and update their respective states.
    3.  If a step in the sequence fails, the Orchestrator executes **compensating transactions** (e.g., rolling back states or deleting draft resources) rather than locking databases with distributed transaction logs.

---

### Q9: Why use TimescaleDB for Alarm History instead of standard PostgreSQL or NoSQL? (ADR-005)
*   **Context**: Alarm logs are high-frequency time-series data. Standard tables degrade in performance when containing millions of records.
*   **Answer**:
    TimescaleDB is built directly on PostgreSQL, allowing us to keep relational integrity, SQL query syntax, and EF Core compatibility. However, it automatically partitions tables into time-based chunks called **hypertables**:
    1.  **Fast Writes & Reads**: It speeds up ingestion rates and isolates queries to specific downtime ranges (e.g., ±10m around the failure).
    2.  **Columnar Compression**: Automatically compresses older historical alarms by ~90%, saving massive storage costs while keeping historical data directly queryable.

---

### Q10: What reverse proxy and forward proxy concepts are used in this design? (Doc 07)
*   **Context**: Differentiating inbound and outbound proxy routing in a zero-trust enterprise network.
*   **Answer**:
    We use a multi-tier proxy architecture to secure network traffic:
    1.  **Inbound (Reverse Proxy Tier)**: We intercept client requests using three layers of reverse proxies:
        *   *Azure Front Door Premium (Global Edge)*: Terminates TLS 1.3, manages SSL, and runs WAF rules (DDoS, SQLi, XSS filtering) at the internet edge.
        *   *Azure API Management (APIM - Gateway)*: Handles JWT signature validation, token checks, and client rate limiting.
        *   *AKS NGINX Ingress Controller (Internal)*: Directs traffic to individual microservices within the private virtual network.
    2.  **Outbound (Forward Proxy Tier)**: We intercept container egress traffic using a single forward proxy:
        *   *Azure Firewall Premium*: Outbound HTTP calls from services (e.g., the AI Gateway calling external LLM endpoints) are forced through the firewall. It performs TLS inspection, checks external certificates, and applies strict FQDN filtering to prevent data exfiltration.
