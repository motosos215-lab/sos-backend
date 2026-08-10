using MotoSOS.API.Modules.TelemetrySummary.Domain;
using MotoSOS.API.Modules.Trips.Domain;

namespace MotoSOS.API.Modules.TelemetrySummary.Application;

public sealed record TelemetrySummaryQuery(string? UserId, string? TripId, TripStatus? TripStatus, TelemetrySummaryStatus? SummaryStatus, DateTimeOffset? DateFrom, DateTimeOffset? DateTo, int PageNumber, int PageSize);
