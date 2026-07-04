# ADR-003: AI Abstraction Gateway

## Status
Accepted

## Date
2026-07-01

## Context
Integrating core business applications directly with third-party Large Language Model (LLM) APIs (like OpenAI, Anthropic, or Cohere) introduces several architectural risks:
*   **API Instability**: LLM vendor APIs change frequently, deprecating models or parameter names.
*   **Vendor Lock-in**: Hardcoding client libraries (e.g., Azure OpenAI SDK) makes it expensive to migrate to other providers.
*   **PII & Safety Risks**: Sending raw manufacturing configurations or operator emails to external clouds violates corporate security and trade secret policies.
*   **Hallucination & Reliability**: LLMs can return malformed JSON or invalid recommendations. We need a way to filter, validate, and safe-guard model output before it hits core databases.

## Decision
We will isolate all AI logic inside a dedicated microservice: the **AI Gateway Service**, implementing the **Strategy** and **Adapter** design patterns.

Core microservices will communicate with the AI Gateway using a standardized, internal JSON schema (`POST /internal/ai/analyze`). The AI Gateway encapsulates:
1.  **Vendor Adapters**: Code written to the `IAIAnalysisProvider` interface that maps internal DTOs to vendor-specific REST requests (Azure OpenAI, AWS Bedrock, or custom local models).
2.  **Prompt Template Engine**: Manages and versions prompts out-of-code.
3.  **PII Sanitizer**: Redacts sensitive data before dispatch.
4.  **Multi-Layer Response Validation**: Runs syntactic, safety, and database cross-reference checks on the output before returning the normalized DTO.

## Alternatives Considered
*   **Direct Core Integration**: Let the Investigation Orchestrator call Azure OpenAI directly using NuGet SDKs. Rejected. This couples the core business workflow to a specific vendor API and scatters prompt management across codebase files.

## Consequences
### Benefits:
*   **Vendor Independence**: Swapping the primary AI model from OpenAI to Anthropic Bedrock requires changing a configuration value in APIM/K8s, with zero changes to core code.
*   **Security Control**: Centralizes data scrubbing rules. We ensure corporate intellectual property is redacted before exiting the network boundary.
*   **Reliability Guard**: Hallucinations and invalid model syntax are filtered at the gate. If a model output fails validation, the gateway handles fallback execution automatically.
*   **Cost Control & Caching**: We can audit token usage and costs at a single point and implement Redis caching for identical context queries.

### Trade-offs:
*   **Performance overhead**: Introduces an extra network hop (Orchestrator → AI Gateway → External AI). We mitigate this by keeping the gateway stateless and deploying it on the same network subnet as the orchestrator.
*   **Complex Contract Mapping**: Requires writing mapper classes in C# to translate each vendor's raw output into the normalized platform contract.
