using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Security.Tokens;

public interface IJwtTokenService
{
    TokenResult CreateAccessToken(User user);
}
