using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Modules.Escalations.Application;
using MotoSOS.API.Modules.Escalations.Contracts;
using MotoSOS.API.Modules.Escalations.Worker;

namespace UnitTest.Escalations;

public sealed class AutomaticEscalationWorkerTests
{
    [Fact]
    public async Task DisabledWorkerDoesNotProcess()
    {
        var service = new FakeAutomaticEscalationService();
        AutomaticEscalationWorker worker = Worker(new AutomaticEscalationWorkerOptions { Enabled = false }, service, out InMemoryAutomaticEscalationWorkerStateStore state);

        await worker.RunOnceAsync(CancellationToken.None);

        service.RunCount.Should().Be(0);
        state.GetSnapshot().LastRunStartedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task EnabledWorkerProcessesUsingConfiguredRequest()
    {
        var service = new FakeAutomaticEscalationService { Response = new RunAutomaticEscalationResponse(2, 1, 1, 0, 0, 1, 0) };
        AutomaticEscalationWorker worker = Worker(new AutomaticEscalationWorkerOptions { Enabled = true, MaxItemsPerRun = 2, EscalateAfterSeconds = 600 }, service, out InMemoryAutomaticEscalationWorkerStateStore state);

        await worker.RunOnceAsync(CancellationToken.None);

        service.RunCount.Should().Be(1);
        service.LastRequest.Should().Be(new RunAutomaticEscalationRequest(2, 600));
        AutomaticEscalationWorkerState snapshot = state.GetSnapshot();
        snapshot.LastRunSucceeded.Should().BeTrue();
        snapshot.LastRunProcessed.Should().Be(2);
        snapshot.LastRunEscalated.Should().Be(1);
        snapshot.LastRunSkipped.Should().Be(1);
    }

    [Fact]
    public async Task RunOnStartupAndIntervalProcessWhenEnabled()
    {
        var startupService = new FakeAutomaticEscalationService();
        AutomaticEscalationWorker startup = Worker(new AutomaticEscalationWorkerOptions { Enabled = true, RunOnStartup = true, IntervalSeconds = 60 }, startupService, out _);
        using var startupCts = new CancellationTokenSource();
        await startup.StartAsync(startupCts.Token);
        await startupService.WaitForRunAsync(TimeSpan.FromSeconds(2));
        await startupCts.CancelAsync();
        await startup.StopAsync(CancellationToken.None);

        var intervalService = new FakeAutomaticEscalationService();
        AutomaticEscalationWorker interval = Worker(new AutomaticEscalationWorkerOptions { Enabled = true, RunOnStartup = false, IntervalSeconds = 1 }, intervalService, out _);
        using var intervalCts = new CancellationTokenSource();
        await interval.StartAsync(intervalCts.Token);
        await intervalService.WaitForRunAsync(TimeSpan.FromSeconds(15));
        await intervalCts.CancelAsync();
        await interval.StopAsync(CancellationToken.None);

        startupService.RunCount.Should().BeGreaterThanOrEqualTo(1);
        intervalService.RunCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ConcurrentRunsAreSkippedInSameInstance()
    {
        var service = new FakeAutomaticEscalationService { BlockUntilReleased = true };
        AutomaticEscalationWorker worker = Worker(new AutomaticEscalationWorkerOptions { Enabled = true }, service, out InMemoryAutomaticEscalationWorkerStateStore state);

        Task first = worker.RunOnceAsync(CancellationToken.None);
        await service.WaitForRunAsync(TimeSpan.FromSeconds(2));
        await worker.RunOnceAsync(CancellationToken.None);

        AutomaticEscalationWorkerState skipped = state.GetSnapshot();
        skipped.IsRunning.Should().BeTrue();
        skipped.LastRunSkipped.Should().Be(1);
        skipped.LastErrorCode.Should().Be("automatic_escalation_worker_overlap");
        service.Release();
        await first;
    }

    [Fact]
    public async Task ControlledFailureUpdatesStateAndFutureRunCanSucceed()
    {
        var service = new FakeAutomaticEscalationService { FailNextRun = true };
        AutomaticEscalationWorker worker = Worker(new AutomaticEscalationWorkerOptions { Enabled = true }, service, out InMemoryAutomaticEscalationWorkerStateStore state);

        await worker.RunOnceAsync(CancellationToken.None);
        state.GetSnapshot().LastErrorCode.Should().Be("automatic_escalation_worker_failed");

        await worker.RunOnceAsync(CancellationToken.None);

        state.GetSnapshot().LastRunSucceeded.Should().BeTrue();
        service.RunCount.Should().Be(2);
    }

    [Fact]
    public async Task CancellationIsRecordedWithoutUnhandledException()
    {
        var service = new FakeAutomaticEscalationService { ThrowCancellation = true };
        AutomaticEscalationWorker worker = Worker(new AutomaticEscalationWorkerOptions { Enabled = true }, service, out InMemoryAutomaticEscalationWorkerStateStore state);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await worker.RunOnceAsync(cts.Token);

        state.GetSnapshot().LastErrorCode.Should().Be("automatic_escalation_worker_cancelled");
    }

    private static AutomaticEscalationWorker Worker(AutomaticEscalationWorkerOptions options, FakeAutomaticEscalationService service, out InMemoryAutomaticEscalationWorkerStateStore state)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAutomaticEscalationService>(service);
        ServiceProvider provider = services.BuildServiceProvider();
        state = new InMemoryAutomaticEscalationWorkerStateStore();
        return new AutomaticEscalationWorker(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(options), state, new Clock());
    }

    private sealed class Clock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

    private sealed class FakeAutomaticEscalationService : IAutomaticEscalationService
    {
        private readonly TaskCompletionSource _runStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RunCount { get; private set; }
        public RunAutomaticEscalationRequest? LastRequest { get; private set; }
        public RunAutomaticEscalationResponse Response { get; set; } = new(1, 1, 0, 0, 0, 0, 0);
        public bool BlockUntilReleased { get; set; }
        public bool FailNextRun { get; set; }
        public bool ThrowCancellation { get; set; }

        public async Task<RunAutomaticEscalationResponse> RunAsync(RunAutomaticEscalationRequest request, string runSource, CancellationToken cancellationToken)
        {
            RunCount++;
            LastRequest = request;
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
        public Task<RunAutomaticEscalationResponse> RunForAdminAsync(string adminUserId, RunAutomaticEscalationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<AutomaticEscalationWorkerStatusResponse> GetWorkerStatusAsync(string adminUserId, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
