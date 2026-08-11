namespace MotoSOS.API.Modules.Auth.AdminBootstrap;

public interface IAdminBootstrapInitializer
{
    Task RunAsync(CancellationToken cancellationToken);
}
