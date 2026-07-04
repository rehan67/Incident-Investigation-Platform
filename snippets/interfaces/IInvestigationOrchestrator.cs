using System;
using System.Threading.Tasks;

namespace Platform.Shared.Contracts.Orchestration;

/// <summary>
/// Defines the coordinator contract for running the investigation workflow saga.
/// </summary>
public interface IInvestigationOrchestrator
{
    /// <summary>
    /// Starts the investigation process for a newly created downtime incident.
    /// </summary>
    /// <param name="incidentId">The unique ID of the reported incident</param>
    /// <returns>The unique ID of the triggered investigation saga</returns>
    Task<Guid> StartInvestigationSagaAsync(Guid incidentId);

    /// <summary>
    /// Executes a compensation flow or registers a manual override action 
    /// if the automated pipeline fails.
    /// </summary>
    /// <param name="investigationId">The ID of the active saga</param>
    /// <param name="reason">The explanation for manual override</param>
    Task TriggerManualOverrideAsync(Guid investigationId, string reason);

    /// <summary>
    /// Checks the status and completion steps of the active saga.
    /// </summary>
    Task<SagaStateDto> GetSagaStateAsync(Guid investigationId);
}

public record SagaStateDto(
    Guid InvestigationId,
    Guid IncidentId,
    string CurrentState,
    DateTime StartedAt,
    SagaStepDto[] Steps
);

public record SagaStepDto(
    string StepName,
    string Status,
    DateTime? CompletedAt,
    string ErrorMessage = ""
);
