namespace MotoSOS.API.Modules.Plans.Domain;

public sealed record PlanDefinition(
    PlanTier Tier,
    string Name,
    string Description,
    bool IsDefault,
    bool IsSelectableInWeb,
    bool IsPaid,
    IReadOnlyList<string> Benefits,
    PlanLimits Limits,
    bool AllowsSmartwatch,
    bool AllowsMonitoredTrip,
    bool AllowsAccidentDetection,
    bool AllowsSosButton,
    bool AllowsEmergencyLocation,
    bool AllowsBasicHistory,
    bool AllowsExtendedHistory,
    bool AllowsAutomaticEscalation,
    bool AllowsReports,
    bool AllowsMultipleDrivers,
    bool AllowsFamilyPanel);
