using System.Globalization;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.EmergencyResolution.Application;
using MotoSOS.API.Modules.EmergencyResolution.Domain;
using MotoSOS.API.Modules.EvidenceAttachments.Contracts;
using MotoSOS.API.Modules.EvidenceAttachments.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.EvidenceAttachments.Application;

public sealed class EvidenceAttachmentService : IEvidenceAttachmentService
{
    private static readonly string[] SensitiveMetadataKeys = ["pass" + "word", "pass" + "wordHash", "access" + "Token", "refresh" + "Token", "tok" + "en", "authorization", "bearer", "device" + "Identifier", "device" + "IdentifierHash", "provider" + "Token", "pay" + "load", "base64", "binary", "fileContent", "imageBytes", "videoBytes", "audioBytes", "email", "phone", "pay" + "ment", "card", "connection" + "String", "sec" + "ret", "stack" + "Trace", "exception", "mon" + "go", "mon" + "godb"];
    private readonly IUserRepository _users;
    private readonly IIncidentRepository _incidents;
    private readonly IAlertDispatchRepository _alerts;
    private readonly IEmergencyResolutionRepository _reports;
    private readonly INotificationDeliveryAttemptRepository _attempts;
    private readonly IMonitorLinkedContactRepository _contacts;
    private readonly IEvidenceAttachmentRepository _evidence;
    private readonly IEvidenceAttachmentIdempotencyKeyFactory _keys;
    private readonly IClock _clock;
    private readonly IAuditLogService? _auditLogs;

    public EvidenceAttachmentService(IUserRepository users, IIncidentRepository incidents, IAlertDispatchRepository alerts, IEmergencyResolutionRepository reports, INotificationDeliveryAttemptRepository attempts, IMonitorLinkedContactRepository contacts, IEvidenceAttachmentRepository evidence, IEvidenceAttachmentIdempotencyKeyFactory keys, IClock clock, IAuditLogService? auditLogs = null)
    {
        _users = users; _incidents = incidents; _alerts = alerts; _reports = reports; _attempts = attempts; _contacts = contacts; _evidence = evidence; _keys = keys; _clock = clock; _auditLogs = auditLogs;
    }

    public async Task<CreateEvidenceAttachmentResponse> CreateForRiderAsync(string userId, CreateEvidenceAttachmentRequest request, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(userId, UserRole.Rider, cancellationToken);
        TargetContext target = await ResolveTargetAsync(request, cancellationToken);
        if (target.OwnerUserId != rider.Id) throw new NotFoundAppException("Evidence target was not found.");
        return await CreateCoreAsync(rider.Id, rider.Id, UserRole.Rider, request, target, cancellationToken);
    }

    public async Task<CreateEvidenceAttachmentResponse> CreateForMonitorAsync(string userId, CreateEvidenceAttachmentRequest request, CancellationToken cancellationToken)
    {
        User monitor = await GetUserAsync(userId, UserRole.Monitor, cancellationToken);
        TargetContext target = await ResolveTargetAsync(request, cancellationToken);
        if (!await IsMonitorAssignedAsync(monitor.Id, target, cancellationToken)) throw new NotFoundAppException("Evidence target was not found.");
        return await CreateCoreAsync(target.OwnerUserId, monitor.Id, UserRole.Monitor, request, target, cancellationToken);
    }

    public async Task<GetEvidenceAttachmentsResponse> ListForRiderAsync(string userId, EvidenceAttachmentQuery query, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(userId, UserRole.Rider, cancellationToken);
        EvidenceAttachmentQuery effective = query.Status.HasValue ? query : query with { Status = EvidenceAttachmentStatus.Registered };
        IReadOnlyList<EvidenceAttachment> items = await _evidence.ListByUserIdAsync(rider.Id, effective, cancellationToken);
        long total = await _evidence.CountByUserIdAsync(rider.Id, effective, cancellationToken);
        return new GetEvidenceAttachmentsResponse(items.Select(ToResponse).ToArray(), effective.PageNumber, effective.PageSize, total);
    }

    public async Task<EvidenceAttachmentResponse> GetForRiderAsync(string userId, string id, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(userId, UserRole.Rider, cancellationToken);
        EvidenceAttachment item = await GetOwnedAsync(rider.Id, id, cancellationToken);
        return ToResponse(item);
    }

    public async Task<EvidenceAttachmentResponse> DeleteForRiderAsync(string userId, string id, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(userId, UserRole.Rider, cancellationToken);
        EvidenceAttachment item = await GetOwnedAsync(rider.Id, id, cancellationToken);
        if (item.Status != EvidenceAttachmentStatus.MarkedDeleted)
        {
            DateTimeOffset now = _clock.UtcNow;
            item.Status = EvidenceAttachmentStatus.MarkedDeleted;
            item.DeletedAtUtc = now;
            item.UpdatedAtUtc = now;
            await _evidence.UpdateAsync(item, cancellationToken);
        }
        await RecordAsync(rider.Id, UserRole.Rider, AuditAction.EvidenceAttachmentDeleted, item, cancellationToken);
        return ToResponse(item);
    }

    public async Task<GetEvidenceAttachmentsResponse> ListForAdminAsync(string userId, EvidenceAttachmentQuery query, CancellationToken cancellationToken)
    {
        await GetUserAsync(userId, UserRole.Admin, cancellationToken);
        IReadOnlyList<EvidenceAttachment> items = await _evidence.ListAsync(query, cancellationToken);
        long total = await _evidence.CountAsync(query, cancellationToken);
        return new GetEvidenceAttachmentsResponse(items.Select(ToResponse).ToArray(), query.PageNumber, query.PageSize, total);
    }

    public async Task<EvidenceAttachmentResponse> GetForAdminAsync(string userId, string id, CancellationToken cancellationToken)
    {
        await GetUserAsync(userId, UserRole.Admin, cancellationToken);
        EvidenceAttachment item = await _evidence.GetByIdAsync(id.Trim(), cancellationToken) ?? throw new EvidenceAttachmentNotAvailableAppException("Evidence attachment is not available.");
        return ToResponse(item);
    }

    private async Task<CreateEvidenceAttachmentResponse> CreateCoreAsync(string ownerUserId, string registeredByUserId, UserRole registeredByRole, CreateEvidenceAttachmentRequest request, TargetContext target, CancellationToken cancellationToken)
    {
        EvidenceType type = Parse<EvidenceType>(request.EvidenceType!);
        DateTimeOffset now = _clock.UtcNow;
        string idempotencyKey = _keys.Create(ownerUserId, NormalizeRequired(request.ClientEvidenceId), target.TargetType.ToString(), target.TargetId);
        var item = new EvidenceAttachment
        {
            UserId = ownerUserId,
            RegisteredByUserId = registeredByUserId,
            RegisteredByRole = registeredByRole,
            TargetType = target.TargetType,
            IncidentId = target.IncidentId,
            AlertDispatchId = target.AlertDispatchId,
            EmergencyResolutionReportId = target.EmergencyResolutionReportId,
            TripId = target.TripId,
            EvidenceType = type,
            Source = Parse<EvidenceSource>(request.Source!),
            Status = EvidenceAttachmentStatus.Registered,
            FileName = NormalizeRequired(request.FileName),
            ContentType = NormalizeRequired(request.ContentType),
            SizeBytes = request.SizeBytes!.Value,
            Sha256Hash = NormalizeOptional(request.Sha256Hash)?.ToLowerInvariant(),
            ClientEvidenceId = NormalizeRequired(request.ClientEvidenceId),
            ClientStorageReference = NormalizeOptional(request.ClientStorageReference),
            StorageProvider = string.IsNullOrWhiteSpace(request.StorageProvider) ? EvidenceStorageProvider.None : Parse<EvidenceStorageProvider>(request.StorageProvider!),
            Description = NormalizeOptional(request.Description),
            CapturedAtUtc = request.CapturedAtUtc!.Value.ToUniversalTime(),
            RegisteredAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            IdempotencyKey = idempotencyKey,
            Metadata = SanitizeMetadata(request.Metadata)
        };
        (EvidenceAttachment saved, bool duplicate) = await _evidence.AddOrGetDuplicateAsync(item, cancellationToken);
        if (!duplicate) await RecordAsync(registeredByUserId, registeredByRole, AuditAction.EvidenceAttachmentRegistered, saved, cancellationToken);
        return new CreateEvidenceAttachmentResponse(ToResponse(saved));
    }

    private async Task<TargetContext> ResolveTargetAsync(CreateEvidenceAttachmentRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.IncidentId))
        {
            Incident incident = await _incidents.GetByIdAsync(request.IncidentId.Trim(), cancellationToken) ?? throw new NotFoundAppException("Evidence target was not found.");
            return new TargetContext(EvidenceTargetType.Incident, incident.Id, incident.UserId, incident.Id, null, null, incident.TripId);
        }
        if (!string.IsNullOrWhiteSpace(request.AlertDispatchId))
        {
            AlertDispatchRequest alert = await _alerts.GetByIdAsync(request.AlertDispatchId.Trim(), cancellationToken) ?? throw new NotFoundAppException("Evidence target was not found.");
            return new TargetContext(EvidenceTargetType.AlertDispatch, alert.Id, alert.UserId, alert.IncidentId, alert.Id, null, alert.TripId);
        }
        if (!string.IsNullOrWhiteSpace(request.EmergencyResolutionReportId))
        {
            EmergencyResolutionReport report = await _reports.GetByIdAsync(request.EmergencyResolutionReportId.Trim(), cancellationToken) ?? throw new NotFoundAppException("Evidence target was not found.");
            return new TargetContext(EvidenceTargetType.EmergencyResolutionReport, report.Id, report.UserId, report.IncidentId, report.AlertDispatchId, report.Id, report.TripId);
        }
        throw new ValidationAppException("Exactly one evidence target is required.");
    }

    private async Task<bool> IsMonitorAssignedAsync(string monitorUserId, TargetContext target, CancellationToken cancellationToken)
    {
        IReadOnlyList<EmergencyContact> contacts = await _contacts.GetActiveLinkedByLinkedUserIdAsync(monitorUserId, cancellationToken);
        if (contacts.Count == 0) return false;
        HashSet<string> contactIds = contacts.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<NotificationDeliveryAttempt> attempts = !string.IsNullOrWhiteSpace(target.AlertDispatchId)
            ? await _attempts.ListByAlertDispatchIdAsync(target.OwnerUserId, target.AlertDispatchId, cancellationToken)
            : await _attempts.ListByIncidentIdAsync(target.OwnerUserId, target.IncidentId!, cancellationToken);
        return attempts.Any(a => contactIds.Contains(a.EmergencyContactId));
    }

    private async Task<User> GetUserAsync(string userId, UserRole role, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != role) throw new ForbiddenAppException("Evidence Attachments API is not available for this role.");
        return user;
    }

    private async Task<EvidenceAttachment> GetOwnedAsync(string userId, string id, CancellationToken cancellationToken)
    {
        EvidenceAttachment? item = await _evidence.GetByIdAsync(id.Trim(), cancellationToken);
        if (item is null || item.UserId != userId) throw new EvidenceAttachmentNotAvailableAppException("Evidence attachment is not available.");
        return item;
    }

    private async Task RecordAsync(string actorUserId, UserRole actorRole, AuditAction action, EvidenceAttachment item, CancellationToken cancellationToken)
    {
        try
        {
            if (_auditLogs is not null) await _auditLogs.RecordAsync(actorUserId, actorRole.ToString(), action, AuditModule.Incidents, "EvidenceAttachment", item.Id, AuditOutcome.Success, null, null, null, new Dictionary<string, string> { ["evidenceAttachmentId"] = item.Id, ["incidentId"] = item.IncidentId ?? string.Empty, ["alertDispatchId"] = item.AlertDispatchId ?? string.Empty, ["emergencyResolutionReportId"] = item.EmergencyResolutionReportId ?? string.Empty, ["evidenceType"] = item.EvidenceType.ToString(), ["source"] = item.Source.ToString(), ["status"] = item.Status.ToString(), ["sizeBytes"] = item.SizeBytes.ToString(CultureInfo.InvariantCulture), ["targetType"] = item.TargetType.ToString(), ["registeredByRole"] = item.RegisteredByRole.ToString() }, cancellationToken);
        }
        catch
        {
        }
    }

    private static Dictionary<string, string> SanitizeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0) return [];
        return metadata.Where(pair => !SensitiveMetadataKeys.Any(key => pair.Key.Contains(key, StringComparison.OrdinalIgnoreCase))).ToDictionary(pair => pair.Key, pair => pair.Value.Length > 200 ? pair.Value[..200] : pair.Value);
    }

    private static TEnum Parse<TEnum>(string value) where TEnum : struct, Enum => Enum.Parse<TEnum>(value, false);
    private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static EvidenceAttachmentResponse ToResponse(EvidenceAttachment e) => new(e.Id, e.UserId, e.RegisteredByUserId, e.RegisteredByRole.ToString(), e.TargetType.ToString(), e.IncidentId, e.AlertDispatchId, e.EmergencyResolutionReportId, e.TripId, e.EvidenceType.ToString(), e.Source.ToString(), e.Status.ToString(), e.FileName, e.ContentType, e.SizeBytes, e.Sha256Hash, e.ClientEvidenceId, e.ClientStorageReference, e.StorageProvider.ToString(), e.Description, e.CapturedAtUtc, e.RegisteredAtUtc, e.CreatedAtUtc, e.UpdatedAtUtc, e.DeletedAtUtc, e.Metadata);
    private sealed record TargetContext(EvidenceTargetType TargetType, string TargetId, string OwnerUserId, string? IncidentId, string? AlertDispatchId, string? EmergencyResolutionReportId, string? TripId);
}
