using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Modules.NotificationOutbox.Application;
using MotoSOS.API.Modules.NotificationOutbox.Contracts;
using MotoSOS.API.Modules.NotificationOutbox.Worker;

namespace UnitTest.NotificationOutbox;

public sealed class NotificationOutboxWorkerTests
{
    [Fact]
    public async Task DisabledWorkerDoesNotProcess()
    {
        var service = new FakeOutboxService();
        NotificationOutboxWorker worker = Worker(new NotificationOutboxWorkerOptions { Enabled = false }, service, out InMemoryNotificationOutboxWorkerStateStore state);

        await worker.RunOnceAsync(CancellationToken.None);

        service.RunCount.Should().Be(0);
        state.GetSnapshot().LastRunStartedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task EnabledWorkerProcessesPreparedUsingConfiguredRequest()
    {
        var service = new FakeOutboxService { Response = new RunNotificationOutboxResponse(2, 1, 1, 0, []) };
        NotificationOutboxWorker worker = Worker(new NotificationOutboxWorkerOptions { Enabled = true, MaxItemsPerRun = 2, SimulateFailures = true, IntervalSeconds = 7 }, service, out InMemoryNotificationOutboxWorkerStateStore state);

        await worker.RunOnceAsync(CancellationToken.None);

        service.RunCount.Should().Be(1);
        service.LastRequest.Should().Be(new RunNotificationOutboxRequest(2, true));
        service.LastIntervalSeconds.Should().Be(7);
        NotificationOutboxWorkerState snapshot = state.GetSnapshot();
        snapshot.LastRunSucceeded.Should().BeTrue();
        snapshot.LastRunProcessed.Should().Be(2);
        snapshot.LastRunSimulatedSent.Should().Be(1);
        snapshot.LastRunFailed.Should().Be(1);
    }

    [Fact]
    public async Task RunOnStartupProcessesWithoutBlockingStart()
    {
        var service = new FakeOutboxService();
        NotificationOutboxWorker worker = Worker(new NotificationOutboxWorkerOptions { Enabled = true, RunOnStartup = true, IntervalSeconds = 60 }, service, out _);
        using var cts = new CancellationTokenSource();

        await worker.StartAsync(cts.Token);
        await service.WaitForRunAsync(TimeSpan.FromSeconds(15));
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        service.RunCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task IntervalProcessesAfterConfiguredDelay()
    {
        var service = new FakeOutboxService();
        NotificationOutboxWorker worker = Worker(new NotificationOutboxWorkerOptions { Enabled = true, RunOnStartup = false, IntervalSeconds = 1 }, service, out _);
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
        var service = new FakeOutboxService { BlockUntilReleased = true };
        NotificationOutboxWorker worker = Worker(new NotificationOutboxWorkerOptions { Enabled = true }, service, out InMemoryNotificationOutboxWorkerStateStore state);

        Task first = worker.RunOnceAsync(CancellationToken.None);
        await service.WaitForRunAsync(TimeSpan.FromSeconds(2));
        await worker.RunOnceAsync(CancellationToken.None);

        NotificationOutboxWorkerState skipped = state.GetSnapshot();
        skipped.IsRunning.Should().BeTrue();
        skipped.LastRunSkipped.Should().Be(1);
        skipped.LastErrorCode.Should().Be("worker_run_overlap");
        service.Release();
        await first;
    }

    [Fact]
    public async Task ControlledFailureUpdatesStateAndFutureRunCanSucceed()
    {
        var service = new FakeOutboxService { FailNextRun = true };
        NotificationOutboxWorker worker = Worker(new NotificationOutboxWorkerOptions { Enabled = true }, service, out InMemoryNotificationOutboxWorkerStateStore state);

        await worker.RunOnceAsync(CancellationToken.None);
        state.GetSnapshot().LastErrorCode.Should().Be("worker_run_failed");

        await worker.RunOnceAsync(CancellationToken.None);

        NotificationOutboxWorkerState snapshot = state.GetSnapshot();
        snapshot.LastRunSucceeded.Should().BeTrue();
        snapshot.LastErrorCode.Should().BeNull();
        service.RunCount.Should().Be(2);
    }

    [Fact]
    public async Task CancellationIsRecordedWithoutUnhandledException()
    {
        var service = new FakeOutboxService { ThrowCancellation = true };
        NotificationOutboxWorker worker = Worker(new NotificationOutboxWorkerOptions { Enabled = true }, service, out InMemoryNotificationOutboxWorkerStateStore state);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await worker.RunOnceAsync(cts.Token);

        state.GetSnapshot().LastErrorCode.Should().Be("worker_cancelled");
    }

    private static NotificationOutboxWorker Worker(NotificationOutboxWorkerOptions options, FakeOutboxService service, out InMemoryNotificationOutboxWorkerStateStore state)
    {
        var services = new ServiceCollection();
        services.AddSingleton<INotificationOutboxService>(service);
        ServiceProvider provider = services.BuildServiceProvider();
        state = new InMemoryNotificationOutboxWorkerStateStore();
        return new NotificationOutboxWorker(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(options), state, new Clock());
    }

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class FakeOutboxService : INotificationOutboxService
    {
        private readonly TaskCompletionSource _runStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RunCount { get; private set; }
        public RunNotificationOutboxRequest? LastRequest { get; private set; }
        public int LastIntervalSeconds { get; private set; }
        public RunNotificationOutboxResponse Response { get; set; } = new(1, 1, 0, 0, []);
        public bool BlockUntilReleased { get; set; }
        public bool FailNextRun { get; set; }
        public bool ThrowCancellation { get; set; }

        public async Task<RunNotificationOutboxResponse> RunWorkerAsync(RunNotificationOutboxRequest request, int intervalSeconds, CancellationToken cancellationToken)
        {
            RunCount++;
            LastRequest = request;
            LastIntervalSeconds = intervalSeconds;
            _runStarted.TrySetResult();
            if (ThrowCancellation) throw new OperationCanceledException(cancellationToken);
            if (FailNextRun)
            {
                FailNextRun = false;
                throw new InvalidOperationException("controlled failure");
            }

            if (BlockUntilReleased) await _release.Task.WaitAsync(cancellationToken);
            return Response;
        }

        public Task WaitForRunAsync(TimeSpan timeout) => _runStarted.Task.WaitAsync(timeout);
        public void Release() => _release.TrySetResult();
        public Task<RunNotificationOutboxResponse> RunAsync(string adminUserId, RunNotificationOutboxRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<GetNotificationOutboxStatusResponse> GetStatusAsync(string adminUserId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<NotificationOutboxWorkerStatusResponse> GetWorkerStatusAsync(string adminUserId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<RetryFailedNotificationOutboxResponse> RetryFailedAsync(string adminUserId, RetryFailedNotificationOutboxRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
