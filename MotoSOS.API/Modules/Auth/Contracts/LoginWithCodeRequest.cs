namespace MotoSOS.API.Modules.Auth.Contracts;

public sealed record LoginWithCodeRequest(string Email, string Code);
