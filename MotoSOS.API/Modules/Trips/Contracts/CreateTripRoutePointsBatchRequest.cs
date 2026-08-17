namespace MotoSOS.API.Modules.Trips.Contracts;

public sealed record CreateTripRoutePointsBatchRequest(IReadOnlyList<CreateTripRoutePointRequest>? Points);
