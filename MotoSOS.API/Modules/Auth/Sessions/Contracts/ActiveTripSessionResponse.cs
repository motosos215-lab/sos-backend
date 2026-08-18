namespace MotoSOS.API.Modules.Auth.Sessions.Contracts;

public sealed record ActiveTripSessionResponse(string Id, string Status, DateTimeOffset StartedAtUtc, string? MobileDeviceId, bool Transferred = false);
