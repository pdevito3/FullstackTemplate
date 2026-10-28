namespace FullstackTemplate.Server.Resources;

using Databases;
using Exceptions;
using MediatR;

/// <summary>
/// Runs an <see cref="IRunsAgainWhenRaced"/> command again when its save loses to another save of the same aggregate.
/// For example, each permission add writes the user row, so two adds on one user at the same time race on that row.
/// The lost save rolled back, so the handler runs again in the same DI scope after
/// <see cref="AppDbContext.DiscardLostRace"/>. The caller, the validation, and the request span stay those of the
/// first run. After <see cref="MaxRuns"/> lost runs, it throws a <see cref="ConcurrentChangeException"/>.
/// </summary>
public sealed class RaceRetryBehavior<TRequest, TResponse>(AppDbContext dbContext, TimeProvider timeProvider)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRunsAgainWhenRaced
{
    internal const int MaxRuns = 20;
    internal const int MaxWaitMs = 250;
    private const int FirstMaxWaitMs = 10;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        for (var run = 1; ; run++)
        {
            try
            {
                return await next(cancellationToken);
            }
            catch (StaleVersionException) when (run < MaxRuns)
            {
                Telemetry.AddEvent([("app.race.run", run)], "race.lost");
                dbContext.DiscardLostRace();
                await Task.Delay(JitteredWaitAfter(run), timeProvider, cancellationToken);
            }
            catch (StaleVersionException exception)
            {
                Telemetry.AddEvent([("app.race.run", run)], "race.exhausted");
                throw new ConcurrentChangeException(exception);
            }
        }
    }

    /// <summary>A random wait up to a limit that doubles with each run, so the requests of one race spread out.</summary>
    private static TimeSpan JitteredWaitAfter(int run)
    {
        var maxWait = Math.Min(MaxWaitMs, FirstMaxWaitMs << Math.Min(run - 1, 10));
        return TimeSpan.FromMilliseconds(Random.Shared.Next(1, maxWait + 1));
    }
}
