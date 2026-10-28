namespace FullstackTemplate.Server.Resources.Extensions;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Throws the cancellation when the caller leaves during a check. A check such as the EF Core DbContext check catches each
/// exception, so the cancellation of the caller becomes Unhealthy and logs an Error. The health service still reports a
/// check that runs past its own timeout as Unhealthy, because it sees that the token of the caller is not cancelled.
/// </summary>
public sealed class CallerCancellationHealthCheck(IHealthCheck check) : IHealthCheck
{
    public static void WrapEachCheck(HealthCheckServiceOptions options)
    {
        foreach (var registration in options.Registrations)
        {
            var factory = registration.Factory;
            registration.Factory = services => new CallerCancellationHealthCheck(factory(services));
        }
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var result = await check.CheckHealthAsync(context, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
