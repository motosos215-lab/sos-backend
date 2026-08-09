using MotoSOS.API.Modules.MinorEvents.Domain;

namespace MotoSOS.API.Modules.MinorEvents.Application;

public sealed record MinorEventQuery(string? UserId, string? TripId, MinorEventType? EventType, MinorEventSeverity? Severity, MinorEventStatus? Status, DateTimeOffset? DateFrom, DateTimeOffset? DateTo, int PageNumber, int PageSize);
