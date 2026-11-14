namespace FullstackTemplate.Server.Resources.Jobs;

using Databases;

/// <summary>
/// A background job of one tenant. Put it on a <c>sealed record</c> with <c>[Job]</c>, and queue it with
/// <see cref="AppDbContext.QueueJob{TJob}"/>, so that it commits with the save.
/// </summary>
public interface ITenantJob
{
    string TenantId { get; }
}
