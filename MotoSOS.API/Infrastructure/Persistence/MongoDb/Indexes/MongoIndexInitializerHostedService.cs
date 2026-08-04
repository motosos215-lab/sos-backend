namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Indexes;

public sealed class MongoIndexInitializerHostedService : IHostedService
{
    private readonly MongoIndexInitializer _initializer;
    private readonly ILogger<MongoIndexInitializerHostedService> _logger;

    public MongoIndexInitializerHostedService(
        MongoIndexInitializer initializer,
        ILogger<MongoIndexInitializerHostedService> logger)
    {
        _initializer = initializer;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _initializer.EnsureIndexesAsync(cancellationToken);
        _logger.LogInformation("MongoDB indexes were ensured successfully.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
