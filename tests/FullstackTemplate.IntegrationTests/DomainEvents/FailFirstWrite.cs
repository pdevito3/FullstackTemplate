namespace FullstackTemplate.IntegrationTests.DomainEvents;

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

/// <summary>Fails the first write command after <see cref="Arm"/> with a transient error, so the execution strategy retries it.</summary>
internal sealed class FailFirstWrite : DbCommandInterceptor
{
    private int _armed;

    public int Failures { get; private set; }

    public void Arm() => Interlocked.Exchange(ref _armed, 1);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        FailWhenArmed(command);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        FailWhenArmed(command);
        return ValueTask.FromResult(result);
    }

    private void FailWhenArmed(DbCommand command)
    {
        var isWrite = command.CommandText.Contains("INSERT INTO", StringComparison.Ordinal)
                      || command.CommandText.Contains("UPDATE ", StringComparison.Ordinal);
        if (!isWrite || Interlocked.Exchange(ref _armed, 0) == 0)
            return;

        Failures++;
        throw new PostgresException("Injected serialization failure.", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure);
    }
}
