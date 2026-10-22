namespace FullstackTemplate.Bff.Tests.Fakes;

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

/// <summary>
/// Fails each command whose SQL holds the tag <paramref name="tag"/>, such as <c>bff_sessions.delete</c>, as a
/// lost database connection does. The error is not transient, so the retries of the execution strategy do not
/// run.
/// </summary>
public sealed class FailingCommandInterceptor(string tag) : DbCommandInterceptor
{
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        FailIfTagged(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        FailIfTagged(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void FailIfTagged(DbCommand command)
    {
        if (command.CommandText.Contains($"-- {tag}", StringComparison.Ordinal))
            throw new NpgsqlException($"A test failed the command {tag}.");
    }
}
