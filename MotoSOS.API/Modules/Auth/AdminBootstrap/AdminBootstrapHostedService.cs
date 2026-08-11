namespace MotoSOS.API.Modules.Auth.AdminBootstrap;

public sealed class AdminBootstrapHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AdminBootstrapHostedService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IAdminBootstrapInitializer>();
        await initializer.RunAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
