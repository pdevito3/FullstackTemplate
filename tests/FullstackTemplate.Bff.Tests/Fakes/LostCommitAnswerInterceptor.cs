namespace FullstackTemplate.Bff.Tests.Fakes;

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

/// <summary>
/// After <see cref="Arm"/>, fails the next commit after Postgres applied it, as when the connection goes away before
/// the answer to <c>COMMIT</c> arrives. Npgsql treats an <see cref="NpgsqlException"/> with an
/// <see cref="IOException"/> inside as transient, so the execution strategy runs the transaction again.
/// </summary>
public sealed class LostCommitAnswerInterceptor : DbTransactionInterceptor
{
    private int _armed;

    public void Arm() => Volatile.Write(ref _armed, 1);

    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default) =>
        Interlocked.Exchange(ref _armed, 0) == 1
            ? throw new NpgsqlException("A test lost the answer to the commit.", new IOException("The connection went away."))
            : Task.CompletedTask;
}
