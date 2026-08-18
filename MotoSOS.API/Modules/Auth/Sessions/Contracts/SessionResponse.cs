namespace MotoSOS.API.Modules.Auth.Sessions.Contracts;

public sealed record SessionResponse(string Id, string SessionType, string DeviceName, string Platform, DateTimeOffset CreatedAtUtc, DateTimeOffset LastSeenAtUtc);
