namespace FullstackTemplate.IntegrationTests.Jobs;

using System.Data.Common;
using FullstackTemplate.Server.Resources.Jobs;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Writes the jobs of the first save in its transaction, then throws the failure, so that transaction does not commit.
/// A transient failure makes the save run again.
/// </summary>
internal sealed class FailAfterJobWrite(IJobWriter writer, Exception failure) : IJobWriter
{
    private int _writes;

    public int Failures { get; private set; }

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
        if (Interlocked.Increment(ref _writes) > 1)
            return first;

        Failures++;
        throw failure;
    }

    public Task<bool> ExistsAsync(Guid jobId, CancellationToken cancellationToken)
        => writer.ExistsAsync(jobId, cancellationToken);
}
