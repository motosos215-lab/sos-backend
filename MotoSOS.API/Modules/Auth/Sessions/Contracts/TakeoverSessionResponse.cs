using MotoSOS.API.Modules.Auth.Contracts;

namespace MotoSOS.API.Modules.Auth.Sessions.Contracts;

public sealed record TakeoverSessionResponse(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAtUtc, SessionResponse Session, ActiveTripSessionResponse? ActiveTrip, AuthUserResponse User);
