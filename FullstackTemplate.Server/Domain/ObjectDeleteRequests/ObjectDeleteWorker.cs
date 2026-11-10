namespace FullstackTemplate.Server.Domain.ObjectDeleteRequests;

using Microsoft.Extensions.Options;
using Resources;
using Serilog;

/// <summary>
/// Runs <see cref="ObjectDeleteRunner"/> each <c>ObjectStorage:Deletes:Interval</c>. It is the only code that deletes
/// the objects of deleted rows. Each Server instance runs it, and the claim keeps two instances off one request.
/// </summary>
public sealed class ObjectDeleteWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ObjectDeleteOptions> options,
    TimeProvider timeProvider) : BackgroundService
{
    private static readonly ILogger Logger = Log.ForContext<ObjectDeleteWorker>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.Interval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<ObjectDeleteRunner>()
                        .RunDueAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The next run tries again. An exception here would stop the host.
                    Telemetry.RecordError(null, ex);
                    Logger.Here().Error(ex, "The run of the object deletes failed");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // A normal stop
        }
    }
}
