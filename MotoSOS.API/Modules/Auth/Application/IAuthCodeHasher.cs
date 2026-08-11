namespace MotoSOS.API.Modules.Auth.Application;

public interface IAuthCodeHasher
{
    string Hash(string code);

    bool Verify(string code, string codeHash);
}
