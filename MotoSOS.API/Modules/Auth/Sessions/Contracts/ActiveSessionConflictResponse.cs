namespace MotoSOS.API.Modules.Auth.Sessions.Contracts;

public sealed record ActiveSessionConflictResponse(ActiveSessionResponse ActiveSession, string TakeoverToken, DateTimeOffset TakeoverExpiresAtUtc, bool HasActiveTrip, ActiveTripSessionResponse? ActiveTrip);
