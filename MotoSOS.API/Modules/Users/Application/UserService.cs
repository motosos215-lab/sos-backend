using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Users.Contracts;

namespace MotoSOS.API.Modules.Users.Application;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _users;

    public UserService(IUserRepository users)
    {
        _users = users;
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(string userId, CancellationToken cancellationToken)
    {
        Domain.User? user = await _users.GetByIdAsync(userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new NotFoundAppException("User was not found.");
        }

        return new CurrentUserResponse(new UserResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.PhoneNumber,
            user.Role.ToString(),
            user.IsActive,
            user.CreatedAtUtc,
            user.UpdatedAtUtc,
            user.LastLoginAtUtc));
    }
}
