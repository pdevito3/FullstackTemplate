namespace FullstackTemplate.Bff.Tests.Fakes;

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

/// <summary>
/// Fails the first command whose SQL holds the tag <paramref name="tag"/>, and each command after it, as a database
/// that goes away at that command does. The error is not transient, so the retries of the execution strategy do not
/// run.
/// </summary>
public sealed class LostDatabaseInterceptor(string tag) : DbCommandInterceptor
{
    private volatile bool _lost;

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        FailIfLost(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        FailIfLost(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        FailIfLost(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void FailIfLost(DbCommand command)
    {
        if (command.CommandText.Split('\n').Any(line => line.TrimEnd('\r') == $"-- {tag}"))
            _lost = true;
        if (_lost)
            throw new NpgsqlException($"A test lost the database at the command {tag}.");
    }
}
