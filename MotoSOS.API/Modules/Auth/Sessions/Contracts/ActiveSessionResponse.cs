namespace MotoSOS.API.Modules.Auth.Sessions.Contracts;

public sealed record ActiveSessionResponse(string SessionType, string DeviceName, string Platform, DateTimeOffset LastSeenAtUtc);
