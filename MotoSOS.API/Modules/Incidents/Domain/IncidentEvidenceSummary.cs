namespace MotoSOS.API.Modules.Incidents.Domain;

public sealed class IncidentEvidenceSummary
{
    public string? AssessmentId { get; set; }
    public string? WindowId { get; set; }
    public IReadOnlyList<string> TriggeredRules { get; set; } = [];
    public bool? HasSmartwatchData { get; set; }
    public bool? HasLocation { get; set; }
    public int? PhoneBatteryLevel { get; set; }
    public int? WatchBatteryLevel { get; set; }
    public string? AppVersion { get; set; }
}
