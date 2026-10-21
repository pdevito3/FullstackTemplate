using System.Diagnostics;
using Microsoft.Extensions.Options;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Sessions;

/// <summary>
/// Deletes the ended BFF sessions in batches, each <c>Bff:Session:CleanupInterval</c>. Each BFF instance runs it.
/// The delete is idempotent, so the instances need no lock.
/// </summary>
public sealed class BffSessionCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<BffSessionOptions> options,
    TimeProvider timeProvider) : BackgroundService
{
    public const int BatchSize = 500;

    private static readonly ILogger Logger = Log.ForContext<BffSessionCleanupService>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.CurrentValue.CleanupInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The next run tries again. An exception here would stop the host.
                Logger.Here().Error(ex, "The cleanup of the ended BFF sessions failed");
            }
        }
    }

    /// <summary>
    /// Deletes all the rows that ended before now, and returns their number.
    /// </summary>
    public async Task<int> CleanupAsync(CancellationToken cancellationToken)
    {
        // The cleanup has no request, so it starts its own trace.
        Activity.Current = null;
        using var activity = Telemetry.Source.StartActivity("bff.sessions.cleanup");
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IBffSessionStore>();
            var now = timeProvider.GetUtcNow();

            var total = 0;
            int deleted;
            do
            {
                deleted = await store.DeleteEndedAsync(now, BatchSize, cancellationToken);
                total += deleted;
            }
            while (deleted == BatchSize);

            activity?.SetTag("app.deleted_count", total);
            if (total > 0)
                Logger.Here().Information("Deleted {Count} ended BFF sessions", total);
            return total;
        }
        catch (Exception ex)
        {
            Telemetry.RecordError(activity, ex);
            throw;
        }
    }
}
