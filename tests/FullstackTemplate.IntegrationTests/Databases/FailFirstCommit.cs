namespace FullstackTemplate.IntegrationTests.Databases;

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

/// <summary>
/// Breaks the connection at the first commit after <see cref="Arm"/>, before the commit or after it. After it, the
/// rows are in, but the caller sees a failure, as when the acknowledgment of the commit is lost.
/// </summary>
internal sealed class FailFirstCommit(bool afterCommit) : DbTransactionInterceptor
{
    private int _armed;

    public int Failures { get; private set; }

    public void Arm() => Interlocked.Exchange(ref _armed, 1);

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (!afterCommit)
            FailWhenArmed();
        return ValueTask.FromResult(result);
    }

    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (afterCommit)
            FailWhenArmed();
        return Task.CompletedTask;
    }

    private void FailWhenArmed()
    {
        if (Interlocked.Exchange(ref _armed, 0) == 0)
            return;

        Failures++;
        throw new NpgsqlException("Injected loss of the connection.", new IOException());
    }
}
