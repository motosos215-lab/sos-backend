namespace MotoSOS.API.Modules.Auth.Contracts;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string ConfirmPassword,
    string FullName,
    string? PhoneNumber,
    string AccountType,
    bool AcceptTerms);
