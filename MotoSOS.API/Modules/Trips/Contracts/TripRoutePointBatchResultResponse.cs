namespace MotoSOS.API.Modules.Trips.Contracts;

public sealed record TripRoutePointBatchResultResponse(
    string ClientRoutePointId,
    string Status,
    string? RoutePointId,
    bool IsDuplicate);
