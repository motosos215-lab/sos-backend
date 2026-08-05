using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.EmergencyContacts.Contracts;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.EmergencyContacts.Application;

public sealed class EmergencyContactService : IEmergencyContactService
{
    private static readonly TimeSpan LinkingCodeLifetime = TimeSpan.FromHours(24);

    private readonly IUserRepository _users;
    private readonly IEmergencyContactRepository _contacts;
    private readonly ILinkingCodeGenerator _codeGenerator;
    private readonly IClock _clock;

    public EmergencyContactService(IUserRepository users, IEmergencyContactRepository contacts, ILinkingCodeGenerator codeGenerator, IClock clock)
    {
        _users = users;
        _contacts = contacts;
        _codeGenerator = codeGenerator;
        _clock = clock;
    }

    public async Task<GetEmergencyContactsResponse> GetMyContactsAsync(string userId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        IReadOnlyList<EmergencyContact> contacts = await _contacts.GetActiveByUserIdAsync(user.Id, cancellationToken);

        return new GetEmergencyContactsResponse(contacts.Select(ToResponse).ToArray());
    }

    public async Task<GetEmergencyContactResponse> GetMyContactAsync(string userId, string contactId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        EmergencyContact contact = await GetOwnedActiveContactAsync(user.Id, contactId, cancellationToken);

        return new GetEmergencyContactResponse(ToResponse(contact));
    }

    public async Task<CreateEmergencyContactResponse> CreateMyContactAsync(string userId, CreateEmergencyContactRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        int activeContacts = await _contacts.CountActiveByUserIdAsync(user.Id, cancellationToken);

        if (activeContacts >= 1)
        {
            throw new PlanLimitExceededAppException("Basic plan allows only one active emergency contact.");
        }

        DateTimeOffset now = _clock.UtcNow;
        var contact = new EmergencyContact
        {
            UserId = user.Id,
            IsActive = true,
            IsPrimary = activeContacts == 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        ApplyChanges(contact, request);
        ApplySaveMode(contact, request.SaveMode);

        await _contacts.AddAsync(contact, cancellationToken);

        return new CreateEmergencyContactResponse(ToResponse(contact));
    }

    public async Task<UpdateEmergencyContactResponse> UpdateMyContactAsync(string userId, string contactId, UpdateEmergencyContactRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        EmergencyContact contact = await GetOwnedActiveContactAsync(user.Id, contactId, cancellationToken);

        ApplyChanges(contact, request);
        ApplySaveMode(contact, request.SaveMode);
        contact.UpdatedAtUtc = _clock.UtcNow;

        await _contacts.UpdateAsync(contact, cancellationToken);

        return new UpdateEmergencyContactResponse(ToResponse(contact));
    }

    public async Task DeleteMyContactAsync(string userId, string contactId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        EmergencyContact contact = await GetOwnedActiveContactAsync(user.Id, contactId, cancellationToken);
        DateTimeOffset now = _clock.UtcNow;

        contact.IsActive = false;
        contact.InvitationStatus = EmergencyContactInvitationStatus.Revoked;
        contact.RevokedAtUtc = now;
        contact.UpdatedAtUtc = now;
        await _contacts.UpdateAsync(contact, cancellationToken);
    }

    public async Task<InviteEmergencyContactResponse> InviteMyContactAsync(string userId, string contactId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        EmergencyContact contact = await GetOwnedActiveContactAsync(user.Id, contactId, cancellationToken);

        if (!HasMinimumData(contact))
        {
            throw new ValidationAppException("Emergency contact must have minimum data before invitation.");
        }

        DateTimeOffset now = _clock.UtcNow;
        string previousCode = contact.LinkingCode ?? string.Empty;
        string linkingCode = _codeGenerator.CreateCode();
        if (linkingCode == previousCode)
        {
            linkingCode = _codeGenerator.CreateCode();
        }

        contact.LinkingCode = linkingCode;
        contact.LinkingCodeExpiresAtUtc = now.Add(LinkingCodeLifetime);
        contact.InvitedAtUtc = now;
        contact.UpdatedAtUtc = now;
        contact.InvitationStatus = EmergencyContactInvitationStatus.Invited;
        await _contacts.UpdateAsync(contact, cancellationToken);

        return new InviteEmergencyContactResponse(new EmergencyContactInvitationResponse(
            contact.Id,
            contact.InvitationStatus.ToString(),
            contact.LinkingCode,
            contact.LinkingCodeExpiresAtUtc.Value));
    }

    public async Task<GetEmergencyContactInvitationResponse> GetInvitationAsync(string code, CancellationToken cancellationToken)
    {
        EmergencyContact? contact = await _contacts.GetByLinkingCodeAsync(code.Trim(), cancellationToken);

        if (contact is null || !contact.IsActive || contact.InvitationStatus == EmergencyContactInvitationStatus.Revoked || contact.LinkingCodeExpiresAtUtc is null || contact.LinkingCodeExpiresAtUtc <= _clock.UtcNow)
        {
            throw new NotFoundAppException("Emergency contact invitation was not found.");
        }

        User? driver = await _users.GetByIdAsync(contact.UserId, cancellationToken);
        if (driver is null || !driver.IsActive)
        {
            throw new NotFoundAppException("Emergency contact invitation was not found.");
        }

        return new GetEmergencyContactInvitationResponse(new EmergencyContactInvitationDetails(
            driver.FullName,
            contact.FullName ?? string.Empty,
            ToPermissionsResponse(contact.Permissions),
            contact.LinkingCodeExpiresAtUtc.Value,
            contact.InvitationStatus.ToString()));
    }

    private async Task<User> GetRiderUserAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAppException("Invalid authentication credentials.");
        }

        if (user.Role != UserRole.Rider)
        {
            throw new ForbiddenAppException("This emergency contacts flow is available only for riders.");
        }

        return user;
    }

    private async Task<EmergencyContact> GetOwnedActiveContactAsync(string userId, string contactId, CancellationToken cancellationToken)
    {
        EmergencyContact? contact = await _contacts.GetByIdAsync(contactId, cancellationToken);

        if (contact is null || !contact.IsActive || contact.UserId != userId)
        {
            throw new NotFoundAppException("Emergency contact was not found.");
        }

        return contact;
    }

    private static void ApplyChanges(EmergencyContact contact, CreateEmergencyContactRequest request)
    {
        contact.FullName = NormalizeOptional(request.FullName);
        contact.Relationship = NormalizeOptional(request.Relationship);
        contact.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        contact.Email = NormalizeOptional(request.Email)?.ToLowerInvariant();
        contact.Priority = request.Priority;
        contact.Permissions = ToDomainPermissions(request.Permissions);
    }

    private static void ApplyChanges(EmergencyContact contact, UpdateEmergencyContactRequest request)
    {
        contact.FullName = NormalizeOptional(request.FullName);
        contact.Relationship = NormalizeOptional(request.Relationship);
        contact.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        contact.Email = NormalizeOptional(request.Email)?.ToLowerInvariant();
        contact.Priority = request.Priority;
        contact.Permissions = ToDomainPermissions(request.Permissions);
    }

    private static void ApplySaveMode(EmergencyContact contact, string? saveMode)
    {
        if (contact.InvitationStatus == EmergencyContactInvitationStatus.Linked)
        {
            return;
        }

        contact.InvitationStatus = string.Equals(saveMode?.Trim(), nameof(SaveMode.Continue), StringComparison.OrdinalIgnoreCase)
            ? EmergencyContactInvitationStatus.Pending
            : EmergencyContactInvitationStatus.Draft;
    }

    private static bool HasMinimumData(EmergencyContact contact) =>
        !string.IsNullOrWhiteSpace(contact.FullName) &&
        !string.IsNullOrWhiteSpace(contact.Relationship) &&
        !string.IsNullOrWhiteSpace(contact.PhoneNumber) &&
        !string.IsNullOrWhiteSpace(contact.Email) &&
        contact.Priority >= 1;

    private static EmergencyContactResponse ToResponse(EmergencyContact contact) => new(
        contact.Id,
        contact.UserId,
        contact.FullName,
        contact.Relationship,
        contact.PhoneNumber,
        contact.Email,
        contact.Priority,
        contact.InvitationStatus.ToString(),
        contact.LinkingCode,
        contact.LinkingCodeExpiresAtUtc,
        contact.LinkedUserId,
        ToPermissionsResponse(contact.Permissions),
        contact.IsPrimary,
        contact.IsActive,
        contact.CreatedAtUtc,
        contact.UpdatedAtUtc,
        contact.InvitedAtUtc,
        contact.LinkedAtUtc,
        contact.RevokedAtUtc);

    private static EmergencyContactPermissions ToDomainPermissions(EmergencyContactPermissionsRequest? permissions) => new()
    {
        CanViewRealTimeLocation = permissions?.CanViewRealTimeLocation ?? false,
        CanReceiveCriticalAlerts = permissions?.CanReceiveCriticalAlerts ?? false,
        CanViewIncidentHistory = permissions?.CanViewIncidentHistory ?? false,
        CanViewVitalSigns = permissions?.CanViewVitalSigns ?? false
    };

    private static EmergencyContactPermissionsResponse ToPermissionsResponse(EmergencyContactPermissions permissions) => new(
        permissions.CanViewRealTimeLocation,
        permissions.CanReceiveCriticalAlerts,
        permissions.CanViewIncidentHistory,
        permissions.CanViewVitalSigns);

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
