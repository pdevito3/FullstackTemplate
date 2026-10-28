namespace FullstackTemplate.FunctionalTests.TestUtilities;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// When a test arms it, waits until the caller leaves. Like the EF Core DbContext check, it reports each exception as
/// Unhealthy, and the cancellation of the caller too.
/// </summary>
public sealed class StallingHealthCheck : IHealthCheck
{
    private TaskCompletionSource? _armed;

    /// <summary>Arms the next check. The task completes when that check starts to wait.</summary>
    public Task ArmNext()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _armed = started;
        return started.Task;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var started = Interlocked.Exchange(ref _armed, null);
        if (started is null)
            return HealthCheckResult.Healthy();

        try
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(exception.Message, exception);
        }
    }
}
