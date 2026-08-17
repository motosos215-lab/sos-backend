namespace MotoSOS.API.Modules.Trips.Application;

public sealed class TripRoutePointOptions
{
    public const string SectionName = "Trips:RoutePoints";

    public int OfflineSyncGraceHours { get; set; } = 24;
    public int MaxBatchSize { get; set; } = 500;
    public int PreviewMaxPoints { get; set; } = 50;
}
