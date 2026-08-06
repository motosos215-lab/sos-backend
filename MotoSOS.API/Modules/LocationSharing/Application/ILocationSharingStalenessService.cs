namespace MotoSOS.API.Modules.LocationSharing.Application;

public interface ILocationSharingStalenessService
{
    bool IsStale(DateTimeOffset recordedAtUtc, DateTimeOffset now);
}
