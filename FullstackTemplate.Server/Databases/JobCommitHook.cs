namespace FullstackTemplate.Server.Databases;

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Writes the queued jobs of a save in the save transaction, just before EF commits it. A job then commits with its rows
/// or not at all, and the SavedChanges interceptors still run after the commit.
/// </summary>
internal sealed class JobCommitHook : DbTransactionInterceptor
{
    public static JobCommitHook Instance { get; } = new();

    private JobCommitHook()
    {
    }

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is AppDbContext context)
            await context.WriteQueuedJobsAsync(transaction, cancellationToken);
        return result;
    }
}
