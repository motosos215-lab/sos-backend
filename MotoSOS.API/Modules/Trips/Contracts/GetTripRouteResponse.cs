namespace MotoSOS.API.Modules.Trips.Contracts;

public sealed record GetTripRouteResponse(
    string TripId,
    string Mode,
    int TotalPoints,
    int ReturnedPoints,
    IReadOnlyList<TripRoutePointResponse> Points);
