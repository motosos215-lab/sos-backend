using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Contracts;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Contracts;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Contracts;
using MotoSOS.API.Modules.SosAlerts.Contracts;

namespace MotoSOS.API.Modules.SosAlerts.Application;

public sealed class CreateSosAlertService : ICreateSosAlertService
{
    private const string IncidentSource = "MobileDetection";
    private readonly IIncidentService _incidents;
    private readonly IAlertDispatchService _alertDispatches;
    private readonly INotificationService _notifications;

    public CreateSosAlertService(IIncidentService incidents, IAlertDispatchService alertDispatches, INotificationService notifications)
    {
        _incidents = incidents;
        _alertDispatches = alertDispatches;
        _notifications = notifications;
    }

    public async Task<CreateSosAlertResponse> CreateAsync(string userId, CreateSosAlertRequest request, CancellationToken cancellationToken)
    {
        CreateIncidentResponse incident = await _incidents.CreateAsync(userId, new CreateIncidentRequest(
            request.TripId,
            request.ClientIncidentId,
            IncidentSource,
            request.IncidentType,
            request.Severity,
            null,
            null,
            null,
            null,
            null,
            request.DetectedAtUtc,
            new IncidentLocationRequest(request.Latitude, request.Longitude, null, null, "MobileGps", request.DetectedAtUtc),
            null), cancellationToken);

        CreateAlertDispatchResponse alertDispatch = await _alertDispatches.CreateAsync(userId, new CreateAlertDispatchRequest(
            incident.Incident.Id,
            request.ClientAlertRequestId,
            request.Priority,
            request.Reason,
            request.DetectedAtUtc,
            request.Notes), cancellationToken);

        PrepareNotificationAttemptsResponse attempts = await _notifications.PrepareAsync(userId, new PrepareNotificationAttemptsRequest(alertDispatch.AlertDispatch.Id, request.Notes), cancellationToken);

        SosAlertNotificationAttemptResponse[] attemptResponses = attempts.Attempts
            .Select(attempt => new SosAlertNotificationAttemptResponse(attempt.Id, attempt.Channel, attempt.Status, attempt.Provider, attempt.EmergencyContactId, attempt.ContactFullName))
            .ToArray();

        return new CreateSosAlertResponse(
            new SosAlertIncidentResponse(incident.Incident.Id, incident.Incident.TripId, incident.Incident.Status, incident.Incident.Cause, incident.Incident.RiskLevel),
            new SosAlertDispatchResponse(alertDispatch.AlertDispatch.Id, alertDispatch.AlertDispatch.IncidentId, alertDispatch.AlertDispatch.Status, alertDispatch.AlertDispatch.ContactsCount),
            attemptResponses,
            new SosAlertSummaryResponse(
                attemptResponses.Count(attempt => attempt.Channel == "Push"),
                attemptResponses.Count(attempt => attempt.Channel == "Sms"),
                attemptResponses.Count(attempt => attempt.Channel == "Email"),
                attemptResponses.Length));
    }
}
