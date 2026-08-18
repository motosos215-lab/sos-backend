using MotoSOS.API.Modules.Auth.Sessions.Contracts;

namespace MotoSOS.API.Modules.Auth.Contracts;

public sealed record LoginResponse(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAtUtc, AuthUserResponse User, SessionResponse? Session = null);
