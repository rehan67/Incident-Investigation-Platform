using System.Threading;
using System.Threading.Tasks;

namespace Platform.Shared.Contracts.Integration;

/// <summary>
/// Defines the contract for an AI provider adapter within the AI Gateway.
/// Implementing this interface isolates core logic from third-party vendor libraries.
/// </summary>
public interface IAIAnalysisProvider
{
    /// <summary>
    /// The unique identifier of the provider implementation (e.g. "AzureOpenAI", "AwsBedrock").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Sends the compiled incident context to the AI model and returns a normalized response.
    /// </summary>
    /// <param name="context">The aggregated incident context package</param>
    /// <param name="promptTemplate">The versioned prompt template to inject the context into</param>
    /// <param name="ct">Token to monitor for API timeouts</param>
    /// <returns>A validated, normalized AI Analysis result</returns>
    Task<NormalizedAnalysisResult> AnalyzeIncidentAsync(
        InvestigationContextDto context,
        string promptTemplate,
        CancellationToken ct
    );

    /// <summary>
    /// Checks the responsiveness and latency of the vendor's API.
    /// </summary>
    Task<ProviderHealthStatus> CheckHealthAsync();
}

public record NormalizedAnalysisResult(
    string RootCause,
    string ConfidenceScore,
    string[] SuggestedSopIds,
    string[] CorrectiveActions,
    bool SafetyReviewRequired
);

public record ProviderHealthStatus(
    bool IsAvailable,
    int LatencyMs,
    string ErrorMessage = ""
);
