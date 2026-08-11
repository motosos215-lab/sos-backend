namespace MotoSOS.API.Modules.Auth.Application;

public interface IAuthCodeGenerator
{
    string CreateCode(int length);
}
