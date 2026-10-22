using System.Diagnostics;
using FullstackTemplate.Bff.BackChannel;
using Microsoft.Extensions.Options;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Sessions;

/// <summary>
/// Deletes the ended BFF sessions and the expired logout tokens of the replay cache in batches, each
/// <c>Bff:Session:CleanupInterval</c>. Each BFF instance runs it.
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
        try
        {
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
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // A normal stop. When the host fails to start, it does not signal a stop, and it logs a cancellation
            // that escapes here as a failed service.
        }
    }

    /// <summary>
    /// Deletes all the BFF sessions that ended before now, and returns their number. Also deletes the expired rows
    /// of the replay cache of the logout tokens.
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

            var total = await DeleteInBatchesAsync(async () =>
            {
                var deleted = await store.DeleteEndedAsync(now, BatchSize, cancellationToken);
                Telemetry.RecordSessionsEnded(SessionEndReason.Expiry, deleted);
                return deleted;
            });

            var replayCache = scope.ServiceProvider.GetRequiredService<LogoutTokenReplayCache>();
            var expiredLogoutTokens = await DeleteInBatchesAsync(() => replayCache.DeleteExpiredAsync(now, BatchSize, cancellationToken));

            activity?.SetTag("app.deleted_count", total);
            activity?.SetTag("app.deleted_logout_token_count", expiredLogoutTokens);
            if (total > 0)
                Logger.Here().Information("Deleted {Count} ended BFF sessions", total);
            if (expiredLogoutTokens > 0)
                Logger.Here().Information("Deleted {Count} expired logout tokens from the replay cache", expiredLogoutTokens);
            return total;
        }
        catch (Exception ex)
        {
            Telemetry.RecordError(activity, ex);
            throw;
        }
    }

    private static async Task<int> DeleteInBatchesAsync(Func<Task<int>> deleteBatch)
    {
        var total = 0;
        int deleted;
        do
        {
            deleted = await deleteBatch();
            total += deleted;
        }
        while (deleted == BatchSize);

        return total;
    }
}
