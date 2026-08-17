namespace MotoSOS.API.Modules.Trips.Contracts;

public sealed record CreateTripRoutePointsBatchResponse(
    string TripId,
    int Received,
    int Accepted,
    int Duplicates,
    int Conflicts,
    IReadOnlyList<TripRoutePointBatchResultResponse> Results);
