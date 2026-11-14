namespace FullstackTemplate.IntegrationTests.Jobs;

using System.Diagnostics;
using BackWave.Core;
using BackWave.Jobs;
using BackWave.Monitor;
using BackWave.Storage;
using BackWave.Storage.InMemory;
using BackWave.Testing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Runs jobs with the handlers of a test Server through BackWave, on a store in memory and a clock that the test
/// moves. As in the Server, each attempt gets a new DI scope.
/// </summary>
public sealed class JobHarness : IAsyncDisposable
{
    private static readonly TimeSpan LongestAttempt = TimeSpan.FromSeconds(30);

    private readonly ScopePerAttempt _services;
    private readonly BackWaveHarness _backWave;

    public JobHarness(IServiceProvider server, RetryPolicy retryPolicy)
    {
        _services = new ScopePerAttempt(server);
        _backWave = new BackWaveHarness(server.GetRequiredService<JobRegistry>(), _services,
            new BackWaveHarnessOptions { RetryPolicy = retryPolicy });
    }

    public DateTimeOffset Now => _backWave.Now;

    /// <summary>The store in memory, to write a job that no enqueue can make.</summary>
    public InMemoryJobStore Store => _backWave.Store;

    /// <summary>The next attempts resolve their handlers from <paramref name="server"/>, as after a change of storage.</summary>
    public void UseServer(IServiceProvider server) => _services.Server = server;

    public async Task<Guid> EnqueueAsync<TJob>(TJob job) where TJob : notnull => await _backWave.EnqueueAsync(job);

    /// <summary>Runs each job that is due now, and waits for the end of each attempt.</summary>
    public async Task RunDueAsync()
    {
        await _backWave.RunDueAsync();
        await SettleAsync();
    }

    /// <summary>
    /// Moves the clock and runs each job that comes due, then waits for the end of each attempt. Move the clock to the
    /// due time of a job, not past it, because a move past the lease of an attempt that still runs ends the lease.
    /// </summary>
    public async Task AdvanceAsync(TimeSpan duration)
    {
        await _backWave.AdvanceAsync(duration);
        await SettleAsync();
    }

    public async Task<JobSnapshot> JobAsync(Guid jobId)
        => await _backWave.Monitor.GetJobAsync(jobId) ?? throw new InvalidOperationException($"Job {jobId} is not in the harness.");

    /// <summary>The failure detail of each failed attempt of the job, oldest first.</summary>
    public async Task<List<string>> FailuresOfAsync(Guid jobId)
        => [.. (await _backWave.Monitor.GetJobHistoryAsync(jobId)).Select(t => t.FailureDetail).OfType<string>()];

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    /// <summary>
    /// The pump of the harness takes the outcome of an attempt only when the handler is done. A handler of the Server
    /// calls a real database or storage, so it ends after the pump returns. Each next pump takes the outcomes that ended.
    /// </summary>
    private async Task SettleAsync()
    {
        var waited = Stopwatch.StartNew();
        while ((await _backWave.Monitor.ListJobsAsync(new JobQuery { State = JobState.Leased })).Count > 0)
        {
            if (waited.Elapsed > LongestAttempt)
                throw new TimeoutException($"An attempt ran for more than {LongestAttempt}.");
            await Task.Delay(TimeSpan.FromMilliseconds(20));
            await _backWave.RunDueAsync();
        }
    }

    /// <summary>BackWave resolves the handler of an attempt with one call, so each call gets a scope of its own.</summary>
    private sealed class ScopePerAttempt(IServiceProvider server) : IServiceProvider, IAsyncDisposable
    {
        private readonly List<AsyncServiceScope> _scopes = [];

        public IServiceProvider Server { get; set; } = server;

        public object? GetService(Type serviceType)
        {
            var scope = Server.CreateAsyncScope();
            _scopes.Add(scope);
            return scope.ServiceProvider.GetService(serviceType);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var scope in _scopes)
                await scope.DisposeAsync();
        }
    }
}
