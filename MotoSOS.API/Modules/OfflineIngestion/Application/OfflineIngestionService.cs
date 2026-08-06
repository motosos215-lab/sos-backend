using System.Text.Json;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.OfflineIngestion.Contracts;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.OfflineIngestion.Application;

public sealed class OfflineIngestionService : IOfflineIngestionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IUserRepository _users;
    private readonly IUserDeviceRepository _devices;
    private readonly ITripRepository _trips;
    private readonly IOfflineIngestionRepository _records;
    private readonly IOfflineIngestionIdempotencyKeyFactory _idempotencyKeys;
    private readonly IPayloadHasher _payloadHasher;
    private readonly IClock _clock;

    public OfflineIngestionService(IUserRepository users, IUserDeviceRepository devices, ITripRepository trips, IOfflineIngestionRepository records, IOfflineIngestionIdempotencyKeyFactory idempotencyKeys, IPayloadHasher payloadHasher, IClock clock)
    {
        _users = users;
        _devices = devices;
        _trips = trips;
        _records = records;
        _idempotencyKeys = idempotencyKeys;
        _payloadHasher = payloadHasher;
        _clock = clock;
    }

    public async Task<OfflineIngestionBatchResponse> IngestBatchAsync(string userId, OfflineIngestionBatchRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        UserDevice mobile = await GetOwnedLinkedMobileAsync(user.Id, NormalizeRequired(request.MobileDeviceId), cancellationToken);
        Trip trip = await GetOwnedTripAsync(user.Id, NormalizeRequired(request.TripId), cancellationToken);
        DateTimeOffset receivedAtUtc = _clock.UtcNow;
        var results = new List<OfflineIngestionItemResultResponse>();

        foreach (OfflineIngestionItemRequest item in request.Items ?? [])
        {
            OfflineIngestionItemType itemType = ParseItemType(item.Type!);
            string itemTypeContract = ToContractType(itemType);
            string idempotencyKey = _idempotencyKeys.Create(user.Id, mobile.Id, trip.Id, itemTypeContract, item.ClientEventId!, item.PayloadVersion!.Value);
            string payloadJson = JsonSerializer.Serialize(item.Payload, SerializerOptions);
            var record = new OfflineIngestionRecord
            {
                UserId = user.Id,
                MobileDeviceId = mobile.Id,
                TripId = trip.Id,
                BatchId = request.BatchId!,
                ClientEventId = item.ClientEventId!,
                Type = itemType,
                PayloadVersion = item.PayloadVersion.Value,
                SchemaVersion = request.SchemaVersion!.Value,
                IdempotencyKey = idempotencyKey,
                AckId = Guid.NewGuid().ToString("D"),
                Payload = payloadJson,
                PayloadHash = _payloadHasher.Hash(payloadJson),
                OccurredAtUtc = item.OccurredAtUtc!.Value,
                ReceivedAtUtc = receivedAtUtc,
                ProcessingStatus = OfflineIngestionProcessingStatus.PendingProcessing,
                CreatedAtUtc = receivedAtUtc,
                UpdatedAtUtc = receivedAtUtc
            };

            (OfflineIngestionRecord persisted, bool isDuplicate) = await _records.AddOrGetDuplicateAsync(record, cancellationToken);
            results.Add(new OfflineIngestionItemResultResponse(
                persisted.ClientEventId,
                ToContractType(persisted.Type),
                (isDuplicate ? OfflineIngestionItemResultStatus.Duplicate : OfflineIngestionItemResultStatus.Accepted).ToString(),
                persisted.AckId,
                persisted.Id,
                isDuplicate));
        }

        return new OfflineIngestionBatchResponse(request.BatchId!, receivedAtUtc, results);
    }

    private async Task<User> GetRiderUserAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != UserRole.Rider) throw new ForbiddenAppException("Offline ingestion is available only for riders.");
        return user;
    }

    private async Task<UserDevice> GetOwnedLinkedMobileAsync(string userId, string mobileDeviceId, CancellationToken cancellationToken)
    {
        UserDevice? device = await _devices.GetByIdAsync(mobileDeviceId, cancellationToken);
        if (device is null || device.UserId != userId) throw new NotFoundAppException("Mobile device was not found.");
        if (!device.IsActive || device.DeviceType != DeviceType.MobileApp || device.LinkStatus != DeviceLinkStatus.Linked) throw new TripNotReadyAppException("Mobile device is not ready for offline ingestion.");
        return device;
    }

    private async Task<Trip> GetOwnedTripAsync(string userId, string tripId, CancellationToken cancellationToken)
    {
        Trip? trip = await _trips.GetByIdAsync(tripId, cancellationToken);
        if (trip is null || trip.UserId != userId) throw new NotFoundAppException("Trip was not found.");
        if (trip.Status is not (TripStatus.Active or TripStatus.Finished)) throw new TripNotReadyAppException("Trip is not ready for offline ingestion.");
        return trip;
    }

    private static OfflineIngestionItemType ParseItemType(string value) => value.Trim().ToLowerInvariant() switch
    {
        "minor-event" => OfflineIngestionItemType.MinorEvent,
        "local-incident" => OfflineIngestionItemType.LocalIncident,
        "alert-dispatch-request" => OfflineIngestionItemType.AlertDispatchRequest,
        _ => throw new ValidationAppException("Unsupported offline ingestion item type.")
    };

    private static string ToContractType(OfflineIngestionItemType type) => type switch
    {
        OfflineIngestionItemType.MinorEvent => "minor-event",
        OfflineIngestionItemType.LocalIncident => "local-incident",
        OfflineIngestionItemType.AlertDispatchRequest => "alert-dispatch-request",
        _ => type.ToString()
    };

    private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;
}
