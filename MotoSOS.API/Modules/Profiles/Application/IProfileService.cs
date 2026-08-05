using MotoSOS.API.Modules.Profiles.Contracts;

namespace MotoSOS.API.Modules.Profiles.Application;

public interface IProfileService
{
    Task<GetMyProfileResponse> GetMyProfileAsync(string userId, CancellationToken cancellationToken);

    Task<UpsertMyProfileResponse> UpsertMyProfileAsync(string userId, UpsertMyProfileRequest request, CancellationToken cancellationToken);
}
