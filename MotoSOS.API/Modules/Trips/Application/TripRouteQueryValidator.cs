using MotoSOS.API.Common.Exceptions;

namespace MotoSOS.API.Modules.Trips.Application;

public sealed class TripRouteQueryValidator
{
    public TripRouteQuery Validate(string? mode, int? pageNumber, int? pageSize, int? maxPoints, int defaultPreviewMaxPoints)
    {
        string normalizedMode = string.IsNullOrWhiteSpace(mode) ? "full" : mode.Trim().ToLowerInvariant();
        if (normalizedMode is not ("full" or "preview")) throw new ValidationAppException("mode must be full or preview.");
        if (pageNumber.HasValue && pageNumber.Value < 1) throw new ValidationAppException("pageNumber must be greater than or equal to 1.");
        if (pageSize.HasValue && (pageSize.Value < 1 || pageSize.Value > 500)) throw new ValidationAppException("pageSize must be between 1 and 500.");
        if (maxPoints.HasValue && (maxPoints.Value < 2 || maxPoints.Value > 500)) throw new ValidationAppException("maxPoints must be between 2 and 500.");

        return new TripRouteQuery(normalizedMode, pageNumber ?? 1, pageSize, maxPoints ?? defaultPreviewMaxPoints);
    }
}

public sealed record TripRouteQuery(string Mode, int PageNumber, int? PageSize, int MaxPoints);
