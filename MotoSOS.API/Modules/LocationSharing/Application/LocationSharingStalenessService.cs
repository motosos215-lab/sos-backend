namespace MotoSOS.API.Modules.LocationSharing.Application;

public sealed class LocationSharingStalenessService : ILocationSharingStalenessService
{
    public bool IsStale(DateTimeOffset recordedAtUtc, DateTimeOffset now) => now - recordedAtUtc > TimeSpan.FromMinutes(5);
}
