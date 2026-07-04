using System;
using System.Collections.Generic;

namespace Platform.Shared.Contracts.Integration;

/// <summary>
/// Aggregated context package representing the exact system state surrounding a downtime window.
/// Hydrated by the orchestrator and consumed by the AI Gateway.
/// </summary>
public record InvestigationContextDto(
    Guid InvestigationId,
    Guid IncidentId,
    IncidentSnapshot Incident,
    EquipmentSnapshot Equipment,
    List<AlarmSnapshot> AlarmHistory,
    List<SopSnapshot> RelevantSops,
    ProductionSnapshot ProductionContext
);

public record IncidentSnapshot(
    Guid IncidentId,
    DateTime DowntimeStart,
    string Description,
    int Severity
);

public record EquipmentSnapshot(
    Guid EquipmentId,
    string AssetTag,
    string Model,
    string Manufacturer,
    string Location,
    List<MaintenanceRecordDto> RecentMaintenance
);

public record MaintenanceRecordDto(
    Guid RecordId,
    string Description,
    DateTime PerformedAt,
    string PartsReplaced,
    double DowntimeHours
);

public record AlarmSnapshot(
    string AlarmCode,
    string Severity,
    string Message,
    DateTime TriggeredAt,
    DateTime? ResolvedAt
);

public record SopSnapshot(
    string SopId,
    string Title,
    string DocumentNumber,
    string StepByStepSummary
);

public record ProductionSnapshot(
    string RunId,
    string LotId,
    string RecipeName,
    double CurrentYield
);
