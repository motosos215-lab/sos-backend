namespace MotoSOS.API.Modules.Trips.Contracts;

public sealed record GetTripsResponse(
    IReadOnlyList<TripResponse> Trips,
    int PageNumber,
    int PageSize,
    long TotalCount);
