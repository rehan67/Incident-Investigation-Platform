using System;

namespace Platform.Shared.Contracts.Events;

/// <summary>
/// Root contract for all Domain Events in the platform.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime Timestamp { get; }
}

public record IncidentCreatedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid IncidentId,
    Guid EquipmentId,
    Guid ReportedBy,
    DateTime DowntimeStart,
    string Description,
    int Severity
) : IDomainEvent;

public record InvestigationContextGatheredEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid InvestigationId,
    Guid IncidentId,
    string AssetTag,
    int TotalAlarmsCollected,
    int TotalSopsCollected
) : IDomainEvent;

public record InvestigationAiCompletedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid InvestigationId,
    Guid IncidentId,
    string ProviderName,
    double ConfidenceScore,
    DateTime ProcessedAt
) : IDomainEvent;

public record InvestigationCompletedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid InvestigationId,
    Guid IncidentId,
    Guid ReportId
) : IDomainEvent;

public record ReportGeneratedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid ReportId,
    Guid InvestigationId,
    Guid IncidentId,
    string Title,
    string StorageUrl
) : IDomainEvent;
