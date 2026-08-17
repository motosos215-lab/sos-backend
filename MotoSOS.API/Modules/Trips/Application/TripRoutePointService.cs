using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Trips.Contracts;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.Trips.Application;

public sealed class TripRoutePointService : ITripRoutePointService
{
    private const int MaxFutureSkewMinutes = 5;

    private readonly IUserRepository _users;
    private readonly ITripRepository _trips;
    private readonly ITripRoutePointRepository _routePoints;
    private readonly TripRoutePointOptions _options;
    private readonly TripRouteQueryValidator _queryValidator;
    private readonly IClock _clock;

    public TripRoutePointService(IUserRepository users, ITripRepository trips, ITripRoutePointRepository routePoints, IOptions<TripRoutePointOptions> options, TripRouteQueryValidator queryValidator, IClock clock)
    {
        _users = users;
        _trips = trips;
        _routePoints = routePoints;
        _options = options.Value;
        _queryValidator = queryValidator;
        _clock = clock;
    }

    public async Task<CreateTripRoutePointsBatchResponse> CreateBatchAsync(string userId, string tripId, CreateTripRoutePointsBatchRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        Trip trip = await GetOwnedTripAsync(user.Id, tripId, cancellationToken);
        EnsureTripAcceptsRoutePoints(trip);

        IReadOnlyList<CreateTripRoutePointRequest> points = request.Points!;
        ValidateRecordedTimesForTrip(trip, points);

        string[] clientIds = points.Select(point => NormalizeClientRoutePointId(point.ClientRoutePointId!)).ToArray();
        IReadOnlyList<TripRoutePoint> existingPoints = await _routePoints.GetByTripIdAndClientIdsAsync(trip.Id, clientIds, cancellationToken);
        Dictionary<string, TripRoutePoint> existingByClientId = existingPoints.ToDictionary(point => point.ClientRoutePointId, StringComparer.OrdinalIgnoreCase);

        var results = new List<TripRoutePointBatchResultResponse>(points.Count);
        foreach (CreateTripRoutePointRequest point in points)
        {
            TripRoutePoint routePoint = ToRoutePoint(user.Id, trip.Id, point);
            if (existingByClientId.TryGetValue(routePoint.ClientRoutePointId, out TripRoutePoint? existing))
            {
                results.Add(ToExistingResult(routePoint, existing));
                continue;
            }

            (TripRoutePoint saved, bool isDuplicate) = await _routePoints.AddOrGetDuplicateAsync(routePoint, cancellationToken);
            if (isDuplicate)
            {
                results.Add(ToExistingResult(routePoint, saved));
                continue;
            }

            results.Add(new TripRoutePointBatchResultResponse(routePoint.ClientRoutePointId, "Accepted", saved.Id, false));
        }

        return new CreateTripRoutePointsBatchResponse(
            trip.Id,
            points.Count,
            results.Count(result => result.Status == "Accepted"),
            results.Count(result => result.Status == "Duplicate"),
            results.Count(result => result.Status == "Conflict"),
            results);
    }

    public async Task<GetTripRouteResponse> GetRouteAsync(string userId, string tripId, string? mode, int? pageNumber, int? pageSize, int? maxPoints, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        Trip trip = await GetOwnedTripAsync(user.Id, tripId, cancellationToken);
        TripRouteQuery query = _queryValidator.Validate(mode, pageNumber, pageSize, maxPoints, Math.Clamp(_options.PreviewMaxPoints, 2, 500));

        IReadOnlyList<TripRoutePoint> allPoints = await _routePoints.ListByUserIdAndTripIdAsync(user.Id, trip.Id, cancellationToken);
        IReadOnlyList<TripRoutePoint> returned = query.Mode == "preview"
            ? Downsample(allPoints, query.MaxPoints)
            : Page(allPoints, query.PageNumber, query.PageSize);

        return new GetTripRouteResponse(trip.Id, query.Mode, allPoints.Count, returned.Count, returned.Select(ToResponse).ToArray());
    }

    private async Task<User> GetRiderUserAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != UserRole.Rider) throw new ForbiddenAppException("Trip route points API is available only for riders.");
        return user;
    }

    private async Task<Trip> GetOwnedTripAsync(string userId, string tripId, CancellationToken cancellationToken)
    {
        Trip? trip = await _trips.GetByIdAsync(tripId.Trim(), cancellationToken);
        if (trip is null || trip.UserId != userId) throw new NotFoundAppException("Trip was not found.");
        return trip;
    }

    private void EnsureTripAcceptsRoutePoints(Trip trip)
    {
        DateTimeOffset now = _clock.UtcNow;
        if (trip.Status == TripStatus.Active) return;
        if (trip.Status == TripStatus.Finished && trip.FinishedAtUtc.HasValue && now <= trip.FinishedAtUtc.Value.AddHours(Math.Max(_options.OfflineSyncGraceHours, 0))) return;
        throw new ValidationAppException("Trip route points can no longer be synchronized.");
    }

    private void ValidateRecordedTimesForTrip(Trip trip, IReadOnlyList<CreateTripRoutePointRequest> points)
    {
        DateTimeOffset latestAllowed = trip.Status == TripStatus.Finished && trip.FinishedAtUtc.HasValue
            ? trip.FinishedAtUtc.Value.AddMinutes(MaxFutureSkewMinutes)
            : _clock.UtcNow.AddMinutes(MaxFutureSkewMinutes);

        foreach (CreateTripRoutePointRequest point in points)
        {
            DateTimeOffset recordedAtUtc = point.RecordedAtUtc!.Value.ToUniversalTime();
            if (recordedAtUtc < trip.StartedAtUtc.ToUniversalTime()) throw new ValidationAppException("recordedAtUtc cannot be before the trip start.");
            if (recordedAtUtc > latestAllowed) throw new ValidationAppException("recordedAtUtc is not coherent with the trip timeline.");
        }
    }

    private TripRoutePoint ToRoutePoint(string userId, string tripId, CreateTripRoutePointRequest point) => new()
    {
        TripId = tripId,
        UserId = userId,
        ClientRoutePointId = NormalizeClientRoutePointId(point.ClientRoutePointId!),
        Sequence = point.Sequence!.Value,
        RecordedAtUtc = point.RecordedAtUtc!.Value.ToUniversalTime(),
        Latitude = point.Latitude!.Value,
        Longitude = point.Longitude!.Value,
        AccuracyMeters = point.AccuracyMeters!.Value,
        SpeedMetersPerSecond = point.SpeedMetersPerSecond,
        BearingDegrees = point.BearingDegrees,
        CreatedAtUtc = _clock.UtcNow,
        Source = TripRoutePointSource.Android,
        AppVersion = NormalizeOptional(point.AppVersion),
        DeviceId = NormalizeOptional(point.DeviceId)
    };

    private static TripRoutePointBatchResultResponse ToExistingResult(TripRoutePoint incoming, TripRoutePoint existing) => SameRelevantData(incoming, existing)
        ? new TripRoutePointBatchResultResponse(existing.ClientRoutePointId, "Duplicate", existing.Id, true)
        : new TripRoutePointBatchResultResponse(existing.ClientRoutePointId, "Conflict", existing.Id, true);

    private static bool SameRelevantData(TripRoutePoint left, TripRoutePoint right) =>
        left.Sequence == right.Sequence
        && left.RecordedAtUtc == right.RecordedAtUtc
        && left.Latitude.Equals(right.Latitude)
        && left.Longitude.Equals(right.Longitude)
        && left.AccuracyMeters.Equals(right.AccuracyMeters)
        && Nullable.Equals(left.SpeedMetersPerSecond, right.SpeedMetersPerSecond)
        && Nullable.Equals(left.BearingDegrees, right.BearingDegrees);

    private static IReadOnlyList<TripRoutePoint> Page(IReadOnlyList<TripRoutePoint> points, int pageNumber, int? pageSize) => pageSize.HasValue
        ? points.Skip((pageNumber - 1) * pageSize.Value).Take(pageSize.Value).ToArray()
        : points;

    private static IReadOnlyList<TripRoutePoint> Downsample(IReadOnlyList<TripRoutePoint> points, int maxPoints)
    {
        if (points.Count <= maxPoints) return points;
        var sampled = new List<TripRoutePoint>(maxPoints) { points[0] };
        int middleCount = maxPoints - 2;
        double step = (points.Count - 2) / (double)(middleCount + 1);
        for (int i = 1; i <= middleCount; i++) sampled.Add(points[1 + (int)Math.Round(i * step)]);
        sampled.Add(points[^1]);
        return sampled.DistinctBy(point => point.Id).OrderBy(point => point.Sequence).ToArray();
    }

    private static TripRoutePointResponse ToResponse(TripRoutePoint point) => new(point.Id, point.ClientRoutePointId, point.Sequence, point.RecordedAtUtc, point.Latitude, point.Longitude, point.AccuracyMeters, point.SpeedMetersPerSecond, point.BearingDegrees);
    private static string NormalizeClientRoutePointId(string value) => Guid.Parse(value.Trim()).ToString("D");
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
