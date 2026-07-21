namespace MotoSOS.API.Security.Tokens;

public interface IRefreshTokenGenerator
{
    string CreateToken();

    string HashToken(string plainValue);
}
