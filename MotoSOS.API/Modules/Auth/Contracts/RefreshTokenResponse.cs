namespace MotoSOS.API.Modules.Auth.Contracts;

public sealed record RefreshTokenResponse(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAtUtc);
