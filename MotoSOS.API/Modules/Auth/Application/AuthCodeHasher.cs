using MotoSOS.API.Security.Hashing;

namespace MotoSOS.API.Modules.Auth.Application;

public sealed class AuthCodeHasher : IAuthCodeHasher
{
    private readonly IPasswordHasher _passwordHasher;

    public AuthCodeHasher(IPasswordHasher passwordHasher)
    {
        _passwordHasher = passwordHasher;
    }

    public string Hash(string code) => _passwordHasher.Hash(code);

    public bool Verify(string code, string codeHash) => _passwordHasher.Verify(code, codeHash);
}
