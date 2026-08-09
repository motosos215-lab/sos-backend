namespace MotoSOS.API.Modules.Trips.Domain;

public sealed class TripLocation
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public string? Provider { get; set; }
    public DateTimeOffset? RecordedAtUtc { get; set; }
}
