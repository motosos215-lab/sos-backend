using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Modules.OfflineProcessing.Application;
using MotoSOS.API.Modules.OfflineProcessing.Contracts;
using MotoSOS.API.Modules.OfflineProcessing.Worker;

namespace UnitTest.OfflineProcessing;

public sealed class OfflineProcessingWorkerTests
{
    [Fact]
    public void OptionsBindFromMobileOfflineProcessingWorkerSection()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mobile:OfflineProcessingWorker:Enabled"] = "true",
                ["Mobile:OfflineProcessingWorker:IntervalSeconds"] = "30",
                ["Mobile:OfflineProcessingWorker:MaxItemsPerRun"] = "20",
                ["Mobile:OfflineProcessingWorker:RunOnStartup"] = "true",
                ["Mobile:OfflineProcessingWorker:RecoveryMinutes"] = "10"
            })
            .Build();
        var services = new ServiceCollection();
        services.Configure<OfflineProcessingWorkerOptions>(configuration.GetSection(OfflineProcessingWorkerOptions.SectionName));

        OfflineProcessingWorkerOptions options = services.BuildServiceProvider().GetRequiredService<IOptions<OfflineProcessingWorkerOptions>>().Value;

        options.Enabled.Should().BeTrue();
        options.IntervalSeconds.Should().Be(30);
        options.MaxItemsPerRun.Should().Be(20);
        options.RunOnStartup.Should().BeTrue();
        options.RecoveryMinutes.Should().Be(10);
    }

    [Fact]
    public async Task DisabledWorkerDoesNotProcess()
    {
        var service = new FakeOfflineProcessingService();
        OfflineProcessingWorker worker = Worker(new OfflineProcessingWorkerOptions { Enabled = false }, service, out InMemoryOfflineProcessingWorkerStateStore state);

        await worker.RunOnceAsync(CancellationToken.None);

        service.RunCount.Should().Be(0);
        state.GetSnapshot().LastRunStartedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task EnabledWorkerProcessesUsingConfiguredLimits()
    {
        var service = new FakeOfflineProcessingService { Response = new RunOfflineProcessingResponse(2, 0, 1, [], 1) };
        OfflineProcessingWorker worker = Worker(new OfflineProcessingWorkerOptions { Enabled = true, MaxItemsPerRun = 2, RecoveryMinutes = 10, IntervalSeconds = 30 }, service, out InMemoryOfflineProcessingWorkerStateStore state);

        await worker.RunOnceAsync(CancellationToken.None);

        service.RunCount.Should().Be(1);
        service.LastMaxItems.Should().Be(2);
        service.LastRecoveryMinutes.Should().Be(10);
        OfflineProcessingWorkerState snapshot = state.GetSnapshot();
        snapshot.LastProcessedCount.Should().Be(2);
        snapshot.LastFailedCount.Should().Be(1);
        snapshot.LastRecoveredCount.Should().Be(1);
    }

    [Fact]
    public async Task RunOnStartupProcessesWithoutBlockingStart()
    {
        var service = new FakeOfflineProcessingService();
        OfflineProcessingWorker worker = Worker(new OfflineProcessingWorkerOptions { Enabled = true, RunOnStartup = true, IntervalSeconds = 60 }, service, out _);
        using var cts = new CancellationTokenSource();

        await worker.StartAsync(cts.Token);
        await service.WaitForRunAsync(TimeSpan.FromSeconds(15));
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        service.RunCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ConcurrentRunsAreSkippedInSameInstance()
    {
        var service = new FakeOfflineProcessingService { BlockUntilReleased = true };
        OfflineProcessingWorker worker = Worker(new OfflineProcessingWorkerOptions { Enabled = true }, service, out InMemoryOfflineProcessingWorkerStateStore state);

        Task first = worker.RunOnceAsync(CancellationToken.None);
        await service.WaitForRunAsync(TimeSpan.FromSeconds(2));
        await worker.RunOnceAsync(CancellationToken.None);

        state.GetSnapshot().LastErrorCode.Should().Be("offline_processing_worker_overlap");
        service.Release();
        await first;
    }

    private static OfflineProcessingWorker Worker(OfflineProcessingWorkerOptions options, FakeOfflineProcessingService service, out InMemoryOfflineProcessingWorkerStateStore state)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOfflineProcessingService>(service);
        ServiceProvider provider = services.BuildServiceProvider();
        state = new InMemoryOfflineProcessingWorkerStateStore();
        return new OfflineProcessingWorker(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(options), state, new Clock());
    }

    private sealed class Clock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

    private sealed class FakeOfflineProcessingService : IOfflineProcessingService
    {
        private readonly TaskCompletionSource _runStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RunCount { get; private set; }
        public int LastMaxItems { get; private set; }
        public int LastRecoveryMinutes { get; private set; }
        public RunOfflineProcessingResponse Response { get; set; } = new(1, 0, 0, []);
        public bool BlockUntilReleased { get; set; }

        public async Task<RunOfflineProcessingResponse> RunWorkerAsync(int maxItems, int recoveryMinutes, CancellationToken cancellationToken)
        {
            RunCount++;
            LastMaxItems = maxItems;
            LastRecoveryMinutes = recoveryMinutes;
            _runStarted.TrySetResult();
            if (BlockUntilReleased) await _release.Task.WaitAsync(cancellationToken);
            return Response;
        }

        public Task WaitForRunAsync(TimeSpan timeout) => _runStarted.Task.WaitAsync(timeout);
        public void Release() => _release.TrySetResult();
        public Task<RunOfflineProcessingResponse> RunAsync(string userId, RunOfflineProcessingRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<GetOfflineProcessingStatusResponse> GetStatusAsync(string userId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<OfflineProcessingWorkerStatusResponse> GetWorkerStatusAsync(string adminUserId, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
