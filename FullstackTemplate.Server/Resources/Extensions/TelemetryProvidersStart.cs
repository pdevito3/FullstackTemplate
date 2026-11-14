namespace FullstackTemplate.Server.Resources.Extensions;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

/// <summary>
/// Builds the OpenTelemetry tracer and meter in <see cref="StartingAsync"/>. The hosted service of OpenTelemetry builds
/// them only in its <c>StartAsync</c>, so the spans of the startup migration, which runs in <c>StartingAsync</c>, would
/// have no listener.
/// </summary>
/// <remarks>
/// The host runs <c>StartingAsync</c> in the order of registration, so register it before each service that records
/// telemetry in <c>StartingAsync</c>.
/// </remarks>
internal sealed class TelemetryProvidersStart(IServiceProvider services) : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        _ = services.GetService<TracerProvider>();
        _ = services.GetService<MeterProvider>();
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
