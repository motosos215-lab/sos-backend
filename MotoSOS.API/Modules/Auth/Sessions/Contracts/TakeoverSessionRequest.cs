namespace MotoSOS.API.Modules.Auth.Sessions.Contracts;

public sealed record TakeoverSessionRequest(string TakeoverToken, ClientDeviceRequest? ClientDevice = null, bool TransferActiveTrip = false, string? MobileDeviceId = null);
