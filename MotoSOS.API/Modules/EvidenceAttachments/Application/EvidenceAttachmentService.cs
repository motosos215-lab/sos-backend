using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
    private readonly IEvidenceFileStorageProvider _storage;
    private readonly EvidenceFileValidator _fileValidator;
    private readonly EvidenceStorageOptionsValidator _storageValidator;
    private readonly EvidenceStorageOptions _storageOptions;
    private readonly IHostEnvironment _environment;
    private readonly IClock _clock;
    private readonly IAuditLogService? _auditLogs;

    public EvidenceAttachmentService(IUserRepository users, IIncidentRepository incidents, IAlertDispatchRepository alerts, IEmergencyResolutionRepository reports, INotificationDeliveryAttemptRepository attempts, IMonitorLinkedContactRepository contacts, IEvidenceAttachmentRepository evidence, IEvidenceAttachmentIdempotencyKeyFactory keys, IEvidenceFileStorageProvider storage, EvidenceFileValidator fileValidator, EvidenceStorageOptionsValidator storageValidator, IOptions<EvidenceStorageOptions> storageOptions, IHostEnvironment environment, IClock clock, IAuditLogService? auditLogs = null)
    {
        _users = users; _incidents = incidents; _alerts = alerts; _reports = reports; _attempts = attempts; _contacts = contacts; _evidence = evidence; _keys = keys; _storage = storage; _fileValidator = fileValidator; _storageValidator = storageValidator; _storageOptions = storageOptions.Value; _environment = environment; _clock = clock; _auditLogs = auditLogs;
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

    public async Task<UploadEvidenceAttachmentResponse> UploadForRiderAsync(string userId, EvidenceUploadCommand command, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(userId, UserRole.Rider, cancellationToken);
        Incident incident = await GetIncidentAsync(command.IncidentId, cancellationToken);
        if (incident.UserId != rider.Id) throw new NotFoundAppException("Incident was not found.");
        return await UploadCoreAsync(rider.Id, rider.Id, UserRole.Rider, command, new TargetContext(EvidenceTargetType.Incident, incident.Id, incident.UserId, incident.Id, null, null, incident.TripId), cancellationToken);
    }

    public async Task<UploadEvidenceAttachmentResponse> UploadForMonitorAsync(string userId, EvidenceUploadCommand command, CancellationToken cancellationToken)
    {
        User monitor = await GetUserAsync(userId, UserRole.Monitor, cancellationToken);
        Incident incident = await GetIncidentAsync(command.IncidentId, cancellationToken);
        var target = new TargetContext(EvidenceTargetType.Incident, incident.Id, incident.UserId, incident.Id, null, null, incident.TripId);
        if (!await IsMonitorAssignedAsync(monitor.Id, target, cancellationToken)) throw new NotFoundAppException("Incident was not found.");
        return await UploadCoreAsync(incident.UserId, monitor.Id, UserRole.Monitor, command, target, cancellationToken);
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

    public async Task<EvidenceAttachmentDownload> DownloadForRiderAsync(string userId, string id, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(userId, UserRole.Rider, cancellationToken);
        EvidenceAttachment item = await GetOwnedAsync(rider.Id, id, cancellationToken);
        return await DownloadCoreAsync(rider.Id, UserRole.Rider, item, cancellationToken);
    }

    public async Task<EvidenceAttachmentDownload> DownloadForMonitorAsync(string userId, string id, CancellationToken cancellationToken)
    {
        User monitor = await GetUserAsync(userId, UserRole.Monitor, cancellationToken);
        EvidenceAttachment item = await _evidence.GetByIdAsync(id.Trim(), cancellationToken) ?? throw new EvidenceAttachmentNotAvailableAppException("Evidence attachment is not available.");
        var target = new TargetContext(item.TargetType, item.IncidentId ?? string.Empty, item.UserId, item.IncidentId, item.AlertDispatchId, item.EmergencyResolutionReportId, item.TripId);
        if (!await IsMonitorAssignedAsync(monitor.Id, target, cancellationToken))
        {
            await RecordDeniedAsync(monitor.Id, UserRole.Monitor, item, cancellationToken);
            throw new ForbiddenAppException("Evidence attachment is not available for this monitor.");
        }
        return await DownloadCoreAsync(monitor.Id, UserRole.Monitor, item, cancellationToken);
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

    public async Task<EvidenceAttachmentDownload> DownloadForAdminAsync(string userId, string id, CancellationToken cancellationToken)
    {
        User admin = await GetUserAsync(userId, UserRole.Admin, cancellationToken);
        EvidenceAttachment item = await _evidence.GetByIdAsync(id.Trim(), cancellationToken) ?? throw new EvidenceAttachmentNotAvailableAppException("Evidence attachment is not available.");
        return await DownloadCoreAsync(admin.Id, UserRole.Admin, item, cancellationToken);
    }

    private async Task<UploadEvidenceAttachmentResponse> UploadCoreAsync(string ownerUserId, string uploadedByUserId, UserRole uploadedByRole, EvidenceUploadCommand command, TargetContext target, CancellationToken cancellationToken)
    {
        EvidenceStorageConfigurationStatus storageStatus = _storageValidator.Validate(_storageOptions);
        if (!storageStatus.Enabled) throw new EvidenceStorageAppException("Evidence storage is disabled.", "evidence_storage_disabled");
        if (!storageStatus.Configured) throw new EvidenceStorageAppException("Evidence storage is not configured.", "evidence_storage_not_configured");
        if (!EvidenceFileValidator.IsSafeClientEvidenceId(command.ClientEvidenceId)) throw new ValidationAppException("ClientEvidenceId is invalid.");

        EvidenceFileValidationResult file = _fileValidator.Validate(command.FileName, command.ContentType, command.SizeBytes, _storageOptions.MaxFileSizeBytes);
        string sha256 = await EvidenceFileValidator.ComputeSha256Async(command.Content, cancellationToken);
        string? clientEvidenceId = NormalizeOptional(command.ClientEvidenceId);
        if (clientEvidenceId is not null)
        {
            EvidenceAttachment? existing = await _evidence.GetByClientEvidenceIdAsync(ownerUserId, target.IncidentId!, clientEvidenceId, cancellationToken);
            if (existing is not null)
            {
                if (!string.Equals(existing.Sha256Hash, sha256, StringComparison.OrdinalIgnoreCase) || existing.SizeBytes != command.SizeBytes || !string.Equals(existing.ContentType, file.ContentType, StringComparison.OrdinalIgnoreCase)) throw new EvidenceUploadConflictAppException("Evidence upload conflicts with an existing client evidence id.");
                return new UploadEvidenceAttachmentResponse(ToResponse(existing), true);
            }
        }

        DateTimeOffset now = _clock.UtcNow;
        EvidenceType type = string.IsNullOrWhiteSpace(command.EvidenceType) ? EvidenceType.Photo : Parse<EvidenceType>(command.EvidenceType.Trim());
        var item = new EvidenceAttachment
        {
            UserId = ownerUserId,
            RegisteredByUserId = uploadedByUserId,
            RegisteredByRole = uploadedByRole,
            TargetType = EvidenceTargetType.Incident,
            IncidentId = target.IncidentId,
            TripId = target.TripId,
            EvidenceType = type,
            Source = uploadedByRole == UserRole.Monitor ? EvidenceSource.MonitorMobileApp : EvidenceSource.RiderMobileApp,
            Status = EvidenceAttachmentStatus.Registered,
            FileName = file.SafeFileName,
            OriginalFileName = command.FileName.Trim(),
            StoredFileName = file.SafeFileName,
            ContentType = file.ContentType,
            SizeBytes = command.SizeBytes,
            Sha256Hash = sha256,
            ClientEvidenceId = clientEvidenceId ?? string.Empty,
            StorageProvider = EvidenceStorageProvider.DigitalOceanSpaces,
            Description = NormalizeOptional(command.Description),
            CapturedAtUtc = now,
            RegisteredAtUtc = now,
            UploadedAtUtc = now,
            UploadedByUserId = uploadedByUserId,
            UploadedByRole = uploadedByRole,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            IdempotencyKey = clientEvidenceId is null ? $"upload:{ownerUserId}:{target.IncidentId}:{Guid.NewGuid():N}" : _keys.Create(ownerUserId, clientEvidenceId, target.TargetType.ToString(), target.TargetId),
            Metadata = clientEvidenceId is null ? [] : new Dictionary<string, string> { ["clientEvidenceId"] = clientEvidenceId }
        };
        item.StorageObjectKey = _fileValidator.CreateObjectKey(_storageOptions.BasePath, _environment.EnvironmentName, target.IncidentId!, item.Id, file.SafeFileName);

        try
        {
            EvidenceFileUploadResult uploaded = await _storage.UploadAsync(new EvidenceFileUploadRequest(command.Content, item.StorageObjectKey, file.ContentType, command.SizeBytes), cancellationToken);
            item.Bucket = uploaded.Bucket;
            item.StorageObjectKey = uploaded.ObjectKey;
            (EvidenceAttachment saved, bool duplicate) = await _evidence.AddOrGetDuplicateAsync(item, cancellationToken);
            if (duplicate) return new UploadEvidenceAttachmentResponse(ToResponse(saved), true);
            await RecordAsync(uploadedByUserId, uploadedByRole, AuditAction.EvidenceAttachmentUploaded, saved, cancellationToken);
            return new UploadEvidenceAttachmentResponse(ToResponse(saved), false);
        }
        catch (EvidenceUploadConflictAppException)
        {
            throw;
        }
        catch (Exception)
        {
            await RecordUploadFailureAsync(uploadedByUserId, uploadedByRole, target.IncidentId!, file.ContentType, command.SizeBytes, cancellationToken);
            throw;
        }
    }

    private async Task<EvidenceAttachmentDownload> DownloadCoreAsync(string actorUserId, UserRole actorRole, EvidenceAttachment item, CancellationToken cancellationToken)
    {
        if (item.Status == EvidenceAttachmentStatus.MarkedDeleted || item.IsDeleted || string.IsNullOrWhiteSpace(item.StorageObjectKey)) throw new EvidenceAttachmentNotAvailableAppException("Evidence attachment is not available.");
        EvidenceFileDownloadResult downloaded = await _storage.DownloadAsync(item.StorageObjectKey, item.ContentType, cancellationToken);
        item.DownloadCount++;
        item.LastDownloadedAtUtc = _clock.UtcNow;
        item.UpdatedAtUtc = item.LastDownloadedAtUtc.Value;
        await _evidence.UpdateAsync(item, cancellationToken);
        await RecordAsync(actorUserId, actorRole, AuditAction.EvidenceAttachmentDownloaded, item, cancellationToken);
        return new EvidenceAttachmentDownload(downloaded.Content, item.ContentType, item.FileName, item.SizeBytes);
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

    private async Task<Incident> GetIncidentAsync(string incidentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(incidentId)) throw new ValidationAppException("IncidentId is required.");
        return await _incidents.GetByIdAsync(incidentId.Trim(), cancellationToken) ?? throw new NotFoundAppException("Incident was not found.");
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

    private async Task RecordUploadFailureAsync(string actorUserId, UserRole actorRole, string incidentId, string contentType, long sizeBytes, CancellationToken cancellationToken)
    {
        try
        {
            if (_auditLogs is not null) await _auditLogs.RecordAsync(actorUserId, actorRole.ToString(), AuditAction.EvidenceAttachmentUploadFailed, AuditModule.Incidents, "EvidenceAttachment", null, AuditOutcome.Failed, "evidence_upload_failed", null, null, new Dictionary<string, string> { ["incidentId"] = incidentId, ["contentType"] = contentType, ["sizeBytes"] = sizeBytes.ToString(CultureInfo.InvariantCulture), ["uploadedByRole"] = actorRole.ToString() }, cancellationToken);
        }
        catch
        {
        }
    }

    private async Task RecordDeniedAsync(string actorUserId, UserRole actorRole, EvidenceAttachment item, CancellationToken cancellationToken)
    {
        try
        {
            if (_auditLogs is not null) await _auditLogs.RecordAsync(actorUserId, actorRole.ToString(), AuditAction.EvidenceAttachmentDownloadDenied, AuditModule.Incidents, "EvidenceAttachment", item.Id, AuditOutcome.Failed, "evidence_download_denied", null, null, new Dictionary<string, string> { ["evidenceAttachmentId"] = item.Id, ["incidentId"] = item.IncidentId ?? string.Empty, ["actorRole"] = actorRole.ToString() }, cancellationToken);
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
