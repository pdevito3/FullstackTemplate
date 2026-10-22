namespace FullstackTemplate.Bff.Tests.Fakes;

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Holds each command whose SQL holds the tag <paramref name="tag"/>, such as <c>bff_sessions.delete</c>, until the
/// test releases the gate. The gate tells the test when a command arrives.
/// </summary>
public sealed class HoldingCommandInterceptor(string tag) : DbCommandInterceptor
{
    public FakeIdentityProvider.RequestGate Gate { get; } = new();

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await HoldIfTaggedAsync(command);
        return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        await HoldIfTaggedAsync(command);
        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private async Task HoldIfTaggedAsync(DbCommand command)
    {
        if (!command.CommandText.Contains($"-- {tag}", StringComparison.Ordinal))
            return;

        Gate.Arrived.TrySetResult();
        await Gate.Released.Task;
    }
}
