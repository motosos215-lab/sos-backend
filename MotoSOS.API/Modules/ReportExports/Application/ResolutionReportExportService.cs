using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.EmergencyResolution.Application;
using MotoSOS.API.Modules.EmergencyResolution.Domain;
using MotoSOS.API.Modules.Escalations.Application;
using MotoSOS.API.Modules.Escalations.Domain;
using MotoSOS.API.Modules.EvidenceAttachments.Application;
using MotoSOS.API.Modules.EvidenceAttachments.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.LocationSharing.Application;
using MotoSOS.API.Modules.LocationSharing.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.ReportExports.Contracts;
using MotoSOS.API.Modules.ReportExports.Domain;
using MotoSOS.API.Modules.TelemetrySummary.Application;
using MotoSOS.API.Modules.TelemetrySummary.Domain;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.ReportExports.Application;

public sealed class ResolutionReportExportService : IResolutionReportExportService
{
    private readonly IUserRepository _users;
    private readonly IIncidentRepository _incidents;
    private readonly ITripRepository _trips;
    private readonly IAlertDispatchRepository _alerts;
    private readonly INotificationDeliveryAttemptRepository _notifications;
    private readonly IAlertAcknowledgementRepository _acknowledgements;
    private readonly ILocationSharingRepository _locations;
    private readonly IEmergencyResolutionRepository _reports;
    private readonly IEmergencyEscalationRepository _escalations;
    private readonly ITelemetrySummaryRepository _telemetry;
    private readonly IEvidenceAttachmentRepository _evidence;
    private readonly IMonitorLinkedContactRepository _contacts;
    private readonly INotificationAttemptMonitorRepository _monitorAttempts;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IAuditLogService? _auditLogs;
    private readonly ILocationSharingStalenessService _staleness;
    private readonly IResolutionReportExportRepository _exports;
    private readonly IResolutionReportExportIdempotencyKeyFactory _keys;
    private readonly IClock _clock;

    public ResolutionReportExportService(IUserRepository users, IIncidentRepository incidents, ITripRepository trips, IAlertDispatchRepository alerts, INotificationDeliveryAttemptRepository notifications, IAlertAcknowledgementRepository acknowledgements, ILocationSharingRepository locations, IEmergencyResolutionRepository reports, IEmergencyEscalationRepository escalations, ITelemetrySummaryRepository telemetry, IEvidenceAttachmentRepository evidence, IMonitorLinkedContactRepository contacts, INotificationAttemptMonitorRepository monitorAttempts, IAuditLogRepository auditLogRepository, IResolutionReportExportRepository exports, IResolutionReportExportIdempotencyKeyFactory keys, ILocationSharingStalenessService staleness, IClock clock, IAuditLogService? auditLogs = null)
    {
        _users = users; _incidents = incidents; _trips = trips; _alerts = alerts; _notifications = notifications; _acknowledgements = acknowledgements; _locations = locations; _reports = reports; _escalations = escalations; _telemetry = telemetry; _evidence = evidence; _contacts = contacts; _monitorAttempts = monitorAttempts; _auditLogRepository = auditLogRepository; _exports = exports; _keys = keys; _staleness = staleness; _clock = clock; _auditLogs = auditLogs;
    }

    public async Task<ResolutionReportExportResponse> ExportForRiderAsync(string userId, string incidentId, ResolutionReportExportType exportType, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(userId, UserRole.Rider, cancellationToken);
        Incident incident = await GetIncidentAsync(incidentId, cancellationToken);
        if (incident.UserId != rider.Id) throw new NotFoundAppException("Incident was not found.");
        return await BuildAndRecordAsync(incident, rider.Id, UserRole.Rider, exportType, cancellationToken);
    }

    public async Task<ResolutionReportExportResponse> ExportForAdminAsync(string userId, string incidentId, ResolutionReportExportType exportType, CancellationToken cancellationToken)
    {
        User admin = await GetUserAsync(userId, UserRole.Admin, cancellationToken);
        Incident incident = await GetIncidentAsync(incidentId, cancellationToken);
        return await BuildAndRecordAsync(incident, admin.Id, UserRole.Admin, exportType, cancellationToken);
    }

    public async Task<ResolutionReportExportResponse> ExportForMonitorAsync(string userId, string notificationDeliveryAttemptId, ResolutionReportExportType exportType, CancellationToken cancellationToken)
    {
        User monitor = await GetUserAsync(userId, UserRole.Monitor, cancellationToken);
        NotificationDeliveryAttempt? attempt = await _monitorAttempts.GetByIdAsync(notificationDeliveryAttemptId.Trim(), cancellationToken);
        if (attempt is null) throw new NotFoundAppException("Alert was not found.");
        IReadOnlyList<EmergencyContact> contacts = await _contacts.GetActiveLinkedByLinkedUserIdAsync(monitor.Id, cancellationToken);
        if (!contacts.Any(c => c.Id == attempt.EmergencyContactId)) throw new NotFoundAppException("Alert was not found.");
        Incident incident = await GetIncidentAsync(attempt.IncidentId, cancellationToken);
        return await BuildAndRecordAsync(incident, monitor.Id, UserRole.Monitor, exportType, cancellationToken);
    }

    public async Task<GetResolutionReportExportsResponse> ListForAdminAsync(string userId, ResolutionReportExportQuery query, CancellationToken cancellationToken)
    {
        await GetUserAsync(userId, UserRole.Admin, cancellationToken);
        IReadOnlyList<ResolutionReportExport> items = await _exports.ListAsync(query, cancellationToken);
        long total = await _exports.CountAsync(query, cancellationToken);
        return new GetResolutionReportExportsResponse(items.Select(ToMetadata).ToArray(), query.PageNumber, query.PageSize, total);
    }

    private async Task<ResolutionReportExportResponse> BuildAndRecordAsync(Incident incident, string requestedByUserId, UserRole requestedByRole, ResolutionReportExportType exportType, CancellationToken cancellationToken)
    {
        EmergencyResolutionReport report = await _reports.GetByIncidentIdAsync(incident.Id, cancellationToken) ?? throw new ReportExportNotAvailableAppException("Resolution report is not available.");
        DateTimeOffset now = _clock.UtcNow;
        Trip? trip = string.IsNullOrWhiteSpace(incident.TripId) ? null : await _trips.GetByIdAsync(incident.TripId, cancellationToken);
        IReadOnlyList<AlertDispatchRequest> dispatches = await _alerts.ListByIncidentIdAsync(incident.UserId, incident.Id, cancellationToken);
        AlertDispatchRequest? alert = ChooseAlert(report, dispatches);
        IReadOnlyList<NotificationDeliveryAttempt> notifications = alert is not null ? await _notifications.ListByAlertDispatchIdAsync(incident.UserId, alert.Id, cancellationToken) : await _notifications.ListByIncidentIdAsync(incident.UserId, incident.Id, cancellationToken);
        IReadOnlyList<AlertAcknowledgement> acknowledgements = alert is not null ? await _acknowledgements.ListByAlertDispatchIdAsync(incident.UserId, alert.Id, cancellationToken) : await _acknowledgements.ListByIncidentIdAsync(incident.UserId, incident.Id, cancellationToken);
        EmergencyLocationSnapshot? location = await _locations.GetLatestByIncidentIdAsync(incident.Id, cancellationToken);
        EmergencyEscalation? escalation = alert is null ? null : await _escalations.GetByAlertDispatchIdAsync(alert.Id, cancellationToken);
        TripTelemetrySummary? telemetry = string.IsNullOrWhiteSpace(incident.TripId) ? null : await _telemetry.GetByTripIdAsync(incident.TripId, cancellationToken);
        EvidenceAttachmentQuery evidenceQuery = new(null, incident.Id, null, null, null, null, null, null, null, 1, 100);
        IReadOnlyList<EvidenceAttachment> evidence = await _evidence.ListByUserIdAsync(incident.UserId, evidenceQuery, cancellationToken);
        ResolutionReportAuditSummaryResponse audit = await BuildAuditSummaryAsync(incident.Id, report.Id, cancellationToken);
        await UpsertMetadataAsync(incident, report, requestedByUserId, requestedByRole, exportType, now, cancellationToken);

        var response = new ResolutionReportExportResponse(
            new ResolutionReportExportHeaderResponse("MotoSOS Emergency Resolution Report", now, incident.Id, report.Id, exportType.ToString(), requestedByRole.ToString()),
            new ResolutionReportIncidentSummaryResponse(incident.Id, incident.Status.ToString(), incident.Cause.ToString(), incident.RiskLevel.ToString(), incident.OccurredAtUtc, incident.ClosedAtUtc, incident.CancelledAtUtc, SafeText(incident.ClosureReason), SafeText(incident.ClosureNotes)),
            new ResolutionReportTripSummaryResponse(trip?.Id, trip?.Status.ToString(), trip?.VehicleId, trip?.StartedAtUtc, trip?.FinishedAtUtc),
            new ResolutionReportAlertSummaryResponse(alert?.Id, alert?.Status.ToString(), alert?.ContactsSnapshot.Count ?? 0, notifications.Count, notifications.Count(n => n.Status == NotificationDeliveryStatus.Prepared), notifications.Count(n => n.Status == NotificationDeliveryStatus.SimulatedSent), notifications.Count(n => n.Status == NotificationDeliveryStatus.Failed), notifications.Count(n => n.Status == NotificationDeliveryStatus.Cancelled)),
            new ResolutionReportAcknowledgementSummaryResponse(acknowledgements.Count, acknowledgements.Count(a => a.Status == AlertAcknowledgementStatus.Acknowledged), acknowledgements.Count(a => a.Status == AlertAcknowledgementStatus.Declined), acknowledgements.Count(a => a.Status == AlertAcknowledgementStatus.Viewed), acknowledgements.Count(a => a.Status == AlertAcknowledgementStatus.Pending), acknowledgements.Where(a => a.AcknowledgedAtUtc.HasValue).Select(a => a.AcknowledgedAtUtc).Min()),
            location is null ? new ResolutionReportLocationSummaryResponse(null, null, null, null) : new ResolutionReportLocationSummaryResponse(location.Latitude, location.Longitude, location.RecordedAtUtc, _staleness.IsStale(location.RecordedAtUtc, now)),
            new ResolutionReportResolutionSummaryResponse(report.Outcome.ToString(), SafeText(report.Notes), report.CreatedAtUtc, report.ResponseTimeSeconds, report.LastKnownLocationWasStale, report.ClosedByRole.ToString()),
            escalation is null ? new ResolutionReportEscalationSummaryResponse(null, null, null, null, null) : new ResolutionReportEscalationSummaryResponse(escalation.Status.ToString(), escalation.Reason.ToString(), escalation.Level.ToString(), escalation.CreatedAtUtc, escalation.ResolvedAtUtc),
            telemetry is null ? null : new ResolutionReportTelemetrySummaryResponse(telemetry.TotalMinorEvents, telemetry.EventsByType, telemetry.EventsBySeverity, telemetry.EventsBySource, telemetry.HardBrakeCount, telemetry.LowBatteryCount, telemetry.PossibleFallLowConfidenceCount, telemetry.AverageConfidence, telemetry.MaxScore, telemetry.AverageScore, telemetry.MinBatteryLevel, telemetry.MaxSpeedKmh),
            evidence.Select(ToEvidence).ToArray(),
            audit);
        await RecordAsync(requestedByUserId, requestedByRole, incident.Id, report.Id, exportType, cancellationToken);
        return response;
    }

    private async Task UpsertMetadataAsync(Incident incident, EmergencyResolutionReport report, string requestedByUserId, UserRole requestedByRole, ResolutionReportExportType exportType, DateTimeOffset now, CancellationToken cancellationToken)
    {
        string key = _keys.Create(incident.UserId, report.Id, exportType.ToString());
        ResolutionReportExport? existing = await _exports.GetByIdempotencyKeyAsync(key, cancellationToken);
        var item = new ResolutionReportExport { Id = existing?.Id ?? MongoDB.Bson.ObjectId.GenerateNewId().ToString(), UserId = incident.UserId, IncidentId = incident.Id, EmergencyResolutionReportId = report.Id, ExportType = exportType, Status = ResolutionReportExportStatus.Generated, RequestedByUserId = requestedByUserId, RequestedByRole = requestedByRole, GeneratedAtUtc = now, CreatedAtUtc = existing?.CreatedAtUtc ?? now, UpdatedAtUtc = now, IdempotencyKey = key };
        await _exports.UpsertAsync(item, cancellationToken);
    }

    private async Task<ResolutionReportAuditSummaryResponse> BuildAuditSummaryAsync(string incidentId, string reportId, CancellationToken cancellationToken)
    {
        AuditLogQuery incidentQuery = new(null, null, null, null, null, incidentId, null, null, 1, 1);
        AuditLogQuery reportQuery = new(null, null, null, null, null, reportId, null, null, 1, 1);
        long total = await _auditLogRepository.CountAsync(incidentQuery, cancellationToken) + await _auditLogRepository.CountAsync(reportQuery, cancellationToken);
        IReadOnlyList<AuditLogEntry> incidentLogs = await _auditLogRepository.ListAsync(incidentQuery, cancellationToken);
        IReadOnlyList<AuditLogEntry> reportLogs = await _auditLogRepository.ListAsync(reportQuery, cancellationToken);
        AuditLogEntry? latest = incidentLogs.Concat(reportLogs).OrderByDescending(a => a.CreatedAtUtc).FirstOrDefault();
        return new ResolutionReportAuditSummaryResponse(total, latest?.Action.ToString(), latest?.CreatedAtUtc);
    }

    private async Task<Incident> GetIncidentAsync(string incidentId, CancellationToken cancellationToken) => await _incidents.GetByIdAsync(incidentId.Trim(), cancellationToken) ?? throw new NotFoundAppException("Incident was not found.");
    private async Task<User> GetUserAsync(string userId, UserRole role, CancellationToken cancellationToken) { User? user = await _users.GetByIdAsync(userId, cancellationToken); if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials."); if (user.Role != role) throw new ForbiddenAppException("Resolution report export is not available for this role."); return user; }
    private static AlertDispatchRequest? ChooseAlert(EmergencyResolutionReport report, IReadOnlyList<AlertDispatchRequest> alerts) => !string.IsNullOrWhiteSpace(report.AlertDispatchId) ? alerts.FirstOrDefault(a => a.Id == report.AlertDispatchId) ?? alerts.OrderByDescending(a => a.CreatedAtUtc).FirstOrDefault() : alerts.OrderByDescending(a => a.CreatedAtUtc).FirstOrDefault();
    private static string? SafeText(string? value) { if (string.IsNullOrWhiteSpace(value)) return null; string trimmed = value.Trim(); if (trimmed.Contains("stack", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("exception", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("secret", StringComparison.OrdinalIgnoreCase)) return null; return trimmed.Length > 1000 ? trimmed[..1000] : trimmed; }
    private static ResolutionReportEvidenceSummaryResponse ToEvidence(EvidenceAttachment e) => new(e.Id, e.TargetType.ToString(), e.EvidenceType.ToString(), e.Source.ToString(), e.Status.ToString(), e.FileName, e.ContentType, e.SizeBytes, e.Sha256Hash, e.RegisteredByRole.ToString(), e.CapturedAtUtc, e.CreatedAtUtc);
    private static ResolutionReportExportMetadataResponse ToMetadata(ResolutionReportExport e) => new(e.Id, e.UserId, e.IncidentId, e.EmergencyResolutionReportId, e.ExportType.ToString(), e.Status.ToString(), e.RequestedByUserId, e.RequestedByRole.ToString(), e.GeneratedAtUtc, e.CreatedAtUtc, e.UpdatedAtUtc);
    private async Task RecordAsync(string actorUserId, UserRole role, string incidentId, string reportId, ResolutionReportExportType exportType, CancellationToken cancellationToken)
    {
        try
        {
            if (_auditLogs is not null) await _auditLogs.RecordAsync(actorUserId, role.ToString(), AuditAction.ResolutionReportExportGenerated, AuditModule.EmergencyResolution, "ResolutionReportExport", reportId, AuditOutcome.Success, null, null, null, new Dictionary<string, string> { ["incidentId"] = incidentId, ["emergencyResolutionReportId"] = reportId, ["exportType"] = exportType.ToString(), ["generatedByRole"] = role.ToString() }, cancellationToken);
        }
        catch
        {
        }
    }
}
