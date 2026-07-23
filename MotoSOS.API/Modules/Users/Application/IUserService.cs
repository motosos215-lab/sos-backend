using MotoSOS.API.Modules.Users.Contracts;

namespace MotoSOS.API.Modules.Users.Application;

public interface IUserService
{
    Task<CurrentUserResponse> GetCurrentUserAsync(string userId, CancellationToken cancellationToken);
}
