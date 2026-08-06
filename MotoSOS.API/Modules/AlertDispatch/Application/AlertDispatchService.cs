using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertDispatch.Contracts;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Contracts;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.AlertDispatch.Application;

public sealed class AlertDispatchService : IAlertDispatchService
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IUserRepository _users;
    private readonly IOnboardingService _onboarding;
    private readonly IIncidentRepository _incidents;
    private readonly IEmergencyContactRepository _contacts;
    private readonly IAlertDispatchRepository _alertDispatches;
    private readonly IAlertDispatchIdempotencyKeyFactory _idempotencyKeys;
    private readonly IClock _clock;

    public AlertDispatchService(IUserRepository users, IOnboardingService onboarding, IIncidentRepository incidents, IEmergencyContactRepository contacts, IAlertDispatchRepository alertDispatches, IAlertDispatchIdempotencyKeyFactory idempotencyKeys, IClock clock)
    {
        _users = users;
        _onboarding = onboarding;
        _incidents = incidents;
        _contacts = contacts;
        _alertDispatches = alertDispatches;
        _idempotencyKeys = idempotencyKeys;
        _clock = clock;
    }

    public async Task<CreateAlertDispatchResponse> CreateAsync(string userId, CreateAlertDispatchRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        await EnsureOnboardingReadyAsync(user.Id, cancellationToken);
        Incident incident = await GetOwnedIncidentAsync(user.Id, NormalizeRequired(request.IncidentId), cancellationToken);
        EnsureIncidentAllowsAlert(incident);
        IReadOnlyList<AlertContactSnapshot> contactsSnapshot = await GetEligibleContactsSnapshotAsync(user.Id, cancellationToken);
        if (contactsSnapshot.Count == 0) throw new AlertNotAllowedAppException("At least one eligible emergency contact is required before preparing alerts.");

        DateTimeOffset now = _clock.UtcNow;
        string clientAlertRequestId = NormalizeRequired(request.ClientAlertRequestId);
        var alertDispatch = new AlertDispatchRequest
        {
            UserId = user.Id,
            IncidentId = incident.Id,
            TripId = incident.TripId,
            VehicleId = incident.VehicleId,
            MobileDeviceId = incident.MobileDeviceId,
            SmartwatchDeviceId = incident.SmartwatchDeviceId,
            ClientAlertRequestId = clientAlertRequestId,
            IdempotencyKey = _idempotencyKeys.Create(user.Id, incident.Id, clientAlertRequestId),
            Priority = ParseEnum<AlertDispatchPriority>(request.Priority)!.Value,
            Reason = ParseEnum<AlertDispatchReason>(request.Reason)!.Value,
            Status = AlertDispatchStatus.PendingDispatch,
            RequestedAtUtc = request.RequestedAtUtc!.Value,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Notes = NormalizeOptional(request.Notes),
            ContactsSnapshot = contactsSnapshot
        };

        (AlertDispatchRequest persisted, _) = await _alertDispatches.AddOrGetDuplicateAsync(alertDispatch, cancellationToken);
        return new CreateAlertDispatchResponse(ToResponse(persisted));
    }

    public async Task<GetAlertDispatchesResponse> ListAsync(string userId, string? status, string? incidentId, int? pageNumber, int? pageSize, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        AlertDispatchStatus? parsedStatus = ParseEnum<AlertDispatchStatus>(status);
        int normalizedPageNumber = Math.Max(pageNumber ?? DefaultPageNumber, 1);
        int normalizedPageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        string? normalizedIncidentId = NormalizeOptional(incidentId);
        IReadOnlyList<AlertDispatchRequest> alertDispatches = await _alertDispatches.ListByUserIdAsync(user.Id, parsedStatus, normalizedIncidentId, normalizedPageNumber, normalizedPageSize, cancellationToken);
        long totalCount = await _alertDispatches.CountByUserIdAsync(user.Id, parsedStatus, normalizedIncidentId, cancellationToken);
        return new GetAlertDispatchesResponse(alertDispatches.Select(ToResponse).ToArray(), normalizedPageNumber, normalizedPageSize, totalCount);
    }

    public async Task<GetAlertDispatchResponse> GetAsync(string userId, string id, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        AlertDispatchRequest alertDispatch = await GetOwnedAlertDispatchAsync(user.Id, id, cancellationToken);
        return new GetAlertDispatchResponse(ToResponse(alertDispatch));
    }

    public async Task<CancelAlertDispatchResponse> CancelAsync(string userId, string id, CancelAlertDispatchRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        AlertDispatchRequest alertDispatch = await GetOwnedAlertDispatchAsync(user.Id, id, cancellationToken);
        if (alertDispatch.Status == AlertDispatchStatus.Completed) throw new AlertDispatchAlreadyCompletedAppException("Alert dispatch is already completed.");
        if (alertDispatch.Status == AlertDispatchStatus.Cancelled) return new CancelAlertDispatchResponse(ToResponse(alertDispatch));

        DateTimeOffset now = _clock.UtcNow;
        alertDispatch.Status = AlertDispatchStatus.Cancelled;
        alertDispatch.CancelledAtUtc = request.ClientCancelledAtUtc ?? now;
        alertDispatch.UpdatedAtUtc = now;
        alertDispatch.Notes = NormalizeOptional(request.Reason) ?? alertDispatch.Notes;
        await _alertDispatches.UpdateAsync(alertDispatch, cancellationToken);
        return new CancelAlertDispatchResponse(ToResponse(alertDispatch));
    }

    private async Task<User> GetRiderUserAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != UserRole.Rider) throw new ForbiddenAppException("Alert Dispatch API is available only for riders.");
        return user;
    }

    private async Task EnsureOnboardingReadyAsync(string userId, CancellationToken cancellationToken)
    {
        OnboardingStatusResponse status = await _onboarding.GetStatusAsync(userId, cancellationToken);
        if (status.CompletedSteps != 7 || status.CurrentStep != "Completed" || !status.IsOperational) throw new OnboardingNotReadyAppException("Onboarding must be completed before preparing alerts.");
    }

    private async Task<Incident> GetOwnedIncidentAsync(string userId, string incidentId, CancellationToken cancellationToken)
    {
        Incident? incident = await _incidents.GetByIdAsync(incidentId, cancellationToken);
        if (incident is null || incident.UserId != userId) throw new NotFoundAppException("Incident was not found.");
        return incident;
    }

    private async Task<AlertDispatchRequest> GetOwnedAlertDispatchAsync(string userId, string id, CancellationToken cancellationToken)
    {
        AlertDispatchRequest? alertDispatch = await _alertDispatches.GetByIdAsync(id, cancellationToken);
        if (alertDispatch is null || alertDispatch.UserId != userId) throw new NotFoundAppException("Alert dispatch was not found.");
        return alertDispatch;
    }

    private async Task<IReadOnlyList<AlertContactSnapshot>> GetEligibleContactsSnapshotAsync(string userId, CancellationToken cancellationToken)
    {
        IReadOnlyList<EmergencyContact> contacts = await _contacts.GetActiveByUserIdAsync(userId, cancellationToken);
        return contacts
            .Where(contact => contact.InvitationStatus is EmergencyContactInvitationStatus.Invited or EmergencyContactInvitationStatus.Linked)
            .OrderBy(contact => contact.Priority ?? int.MaxValue)
            .Select(contact => new AlertContactSnapshot { EmergencyContactId = contact.Id, FullName = contact.FullName, PhoneNumber = contact.PhoneNumber, Email = contact.Email, Relationship = contact.Relationship, Priority = contact.Priority, InvitationStatus = contact.InvitationStatus })
            .ToArray();
    }

    private static void EnsureIncidentAllowsAlert(Incident incident)
    {
        if (incident.Status == IncidentStatus.Closed) throw new IncidentNotReadyAppException("Closed incidents cannot prepare alert dispatches.");
        if (incident.Status == IncidentStatus.FalsePositiveCancelled) throw new AlertNotAllowedAppException("False positive incidents cannot prepare alert dispatches.");
        if (incident.Status != IncidentStatus.Open) throw new AlertNotAllowedAppException("Incident cannot prepare alert dispatches.");
    }

    private static AlertDispatchResponse ToResponse(AlertDispatchRequest alertDispatch) => new(alertDispatch.Id, alertDispatch.IncidentId, alertDispatch.TripId, alertDispatch.VehicleId, alertDispatch.MobileDeviceId, alertDispatch.SmartwatchDeviceId, alertDispatch.Priority.ToString(), alertDispatch.Reason.ToString(), alertDispatch.Status.ToString(), alertDispatch.RequestedAtUtc, alertDispatch.CreatedAtUtc, alertDispatch.UpdatedAtUtc, alertDispatch.CancelledAtUtc, alertDispatch.CompletedAtUtc, alertDispatch.Notes, alertDispatch.ContactsSnapshot.Count);
    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct => Enum.TryParse(value, ignoreCase: true, out TEnum result) ? result : null;
    private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
