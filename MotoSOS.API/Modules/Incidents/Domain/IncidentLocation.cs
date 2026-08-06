namespace MotoSOS.API.Modules.Incidents.Domain;

public sealed class IncidentLocation
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public double? SpeedKmh { get; set; }
    public string? Provider { get; set; }
    public DateTimeOffset? RecordedAtUtc { get; set; }
}
