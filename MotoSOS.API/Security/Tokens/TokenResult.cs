namespace MotoSOS.API.Security.Tokens;

public sealed record TokenResult(string AccessToken, DateTimeOffset ExpiresAtUtc);
