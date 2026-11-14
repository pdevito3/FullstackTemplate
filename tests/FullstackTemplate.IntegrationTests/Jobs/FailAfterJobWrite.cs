namespace FullstackTemplate.IntegrationTests.Jobs;

using System.Data.Common;
using FullstackTemplate.Server.Resources.Jobs;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Writes the jobs of the first save after <see cref="Arm"/> in its transaction, then throws the failure, so that
/// transaction does not commit. A transient failure makes the save run again.
/// </summary>
internal sealed class FailAfterJobWrite(IJobWriter writer, Exception failure) : IJobWriter
{
    private int _armed;

    public int Failures { get; private set; }

    /// <summary>Arm it after the arrange. A save of the arrange can write jobs too, as a domain event handler does.</summary>
    public void Arm() => Interlocked.Exchange(ref _armed, 1);

    /// <summary>The services of the tests, with this writer around the job writer of the Server.</summary>
    public static ServiceProvider Around(Exception failure) => TestFixture.BuildServiceProvider(services =>
    {
        var serverWriter = services.Single(d => d.ServiceType == typeof(IJobWriter));
        services.Remove(serverWriter);
        services.AddSingleton<IJobWriter>(provider => new FailAfterJobWrite(
            (IJobWriter)ActivatorUtilities.CreateInstance(provider, serverWriter.ImplementationType!), failure));
    });

    public async Task<Guid> WriteAsync(IReadOnlyList<PendingJob> jobs, DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var first = await writer.WriteAsync(jobs, transaction, cancellationToken);
        if (Interlocked.Exchange(ref _armed, 0) == 0)
            return first;

        Failures++;
        throw failure;
    }

    public Task<bool> ExistsAsync(Guid jobId, CancellationToken cancellationToken)
        => writer.ExistsAsync(jobId, cancellationToken);
}
