namespace MotoSOS.API.Modules.Auth.Contracts;

public sealed record ResetPasswordRequest(string Email, string Code, string NewPassword);
