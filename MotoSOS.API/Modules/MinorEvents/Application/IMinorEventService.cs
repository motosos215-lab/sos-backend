using MotoSOS.API.Modules.MinorEvents.Contracts;

namespace MotoSOS.API.Modules.MinorEvents.Application;

public interface IMinorEventService
{
    Task<CreateMinorEventResponse> CreateAsync(string userId, CreateMinorEventRequest request, CancellationToken cancellationToken);
    Task<CreateMinorEventResponse> CreateFromOfflineAsync(string userId, string offlineIngestionRecordId, string fallbackTripId, string fallbackClientEventId, string? fallbackMobileDeviceId, DateTimeOffset fallbackOccurredAtUtc, CreateMinorEventRequest request, CancellationToken cancellationToken);
    Task<GetMinorEventResponse> GetForRiderAsync(string userId, string id, CancellationToken cancellationToken);
    Task<GetMinorEventsResponse> ListForRiderAsync(string userId, MinorEventQuery query, CancellationToken cancellationToken);
    Task<GetMinorEventsResponse> ListForAdminAsync(string userId, MinorEventQuery query, CancellationToken cancellationToken);
    Task<GetMinorEventResponse> MarkReviewedAsync(string userId, string id, CancellationToken cancellationToken);
    Task<GetMinorEventResponse> IgnoreAsync(string userId, string id, CancellationToken cancellationToken);
}
