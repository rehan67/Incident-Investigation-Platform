# ADR-006: Decision Against Service Mesh (Istio / Linkerd)

## Status
Accepted

## Date
2026-07-01

## Context
Microservice architectures require core infrastructure capabilities like:
*   Service discovery and DNS resolution.
*   Client-side load balancing.
*   Resilience patterns (retries, circuit breakers, timeouts).
*   Service-to-service encryption (mTLS).
*   Distributed tracing and telemetry logs.

Service meshes (such as Istio, Linkerd, or Consul) solve these concerns by injecting proxy sidecar containers (like Envoy) into every application pod. While powerful, they add complexity and latency. We need to decide whether to adopt a service mesh for the platform's 11 microservices.

## Decision
We will **NOT** adopt a Service Mesh at this stage of the platform's lifecycle. 

Instead, we will address these capabilities using native Kubernetes features and C# application libraries:
1.  **Service Discovery & Load Balancing**: Native Kubernetes Services and CoreDNS.
2.  **Resilience Patterns**: **Polly** (`Microsoft.Extensions.Http.Resilience`) integrated directly into the C# HttpClient pipelines.
3.  **Service-to-Service Security**: Kubernetes Network Policies to restrict pod traffic + HTTPS endpoints.
4.  **Distributed Tracing**: Native **OpenTelemetry SDK** integration inside ASP.NET Core runtimes.

## Alternatives Considered
*   **Istio Service Mesh**: Considered and rejected. Istio would manage mTLS and retries globally out of application code. However, it requires deploying and managing a complex control plane, increases pod memory consumption by 50-100MB per instance, and adds 1-3ms latency overhead per hop.

## Consequences
### Benefits:
*   **Operational Simplicity**: No service mesh control plane to manage, upgrade, or troubleshoot.
*   **Low Compute Overhead**: Reduces CPU and memory footprint across nodes, lowering Azure AKS monthly hosting costs by approximately 15-20%.
*   **Fine-Grained Code Control**: Defining retries and circuit breakers in Polly code allows developers to configure distinct parameters per endpoint, rather than applying sweeping, blind network-level rules.
*   **Lower Latency**: Service-to-service communication is direct without routing through dual Envoy sidecars.

### Trade-offs:
*   **Language Dependency**: Resilience policies must be written in the application code (C# / .NET 10). If we introduce non-.NET microservices in the future, policies must be re-implemented in those runtimes.
*   **Developer Responsibility**: Security and tracing configuration require developer awareness (instrumenting OTel tracers, managing TLS certificates), rather than being handled transparently at the platform tier.
