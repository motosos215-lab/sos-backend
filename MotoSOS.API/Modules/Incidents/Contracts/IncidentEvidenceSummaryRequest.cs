namespace MotoSOS.API.Modules.Incidents.Contracts;

public sealed record IncidentEvidenceSummaryRequest(string? AssessmentId, string? WindowId, IReadOnlyList<string>? TriggeredRules, bool? HasSmartwatchData, bool? HasLocation, int? PhoneBatteryLevel, int? WatchBatteryLevel, string? AppVersion);
