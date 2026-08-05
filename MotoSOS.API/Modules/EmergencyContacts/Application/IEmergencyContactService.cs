using MotoSOS.API.Modules.EmergencyContacts.Contracts;

namespace MotoSOS.API.Modules.EmergencyContacts.Application;

public interface IEmergencyContactService
{
    Task<GetEmergencyContactsResponse> GetMyContactsAsync(string userId, CancellationToken cancellationToken);

    Task<GetEmergencyContactResponse> GetMyContactAsync(string userId, string contactId, CancellationToken cancellationToken);

    Task<CreateEmergencyContactResponse> CreateMyContactAsync(string userId, CreateEmergencyContactRequest request, CancellationToken cancellationToken);

    Task<UpdateEmergencyContactResponse> UpdateMyContactAsync(string userId, string contactId, UpdateEmergencyContactRequest request, CancellationToken cancellationToken);

    Task DeleteMyContactAsync(string userId, string contactId, CancellationToken cancellationToken);

    Task<InviteEmergencyContactResponse> InviteMyContactAsync(string userId, string contactId, CancellationToken cancellationToken);

    Task<GetEmergencyContactInvitationResponse> GetInvitationAsync(string code, CancellationToken cancellationToken);
}
