namespace FullstackTemplate.SharedTestHelpers.Utilities;

using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

/// <summary>
/// Holds the first command whose SQL holds a query tag, such as <c>users.add_permission.load</c>, until the test calls
/// <see cref="Release"/>. Thus a test can put a rival request at one step of the request under test. See
/// <c>docs/backend.md</c>, section "Race tests".
/// </summary>
/// <remarks>
/// The Bff tests compile this file with a link, because a reference to this project also brings in the Server.
/// </remarks>
public sealed class PauseAtQuery : DbCommandInterceptor
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly string _tag;
    private readonly bool _after;
    private readonly TimeSpan _timeout;
    private readonly Regex _tagInSql;
    private readonly TaskCompletionSource<NpgsqlConnection> _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _claimed;

    private PauseAtQuery(string tag, bool after, TimeSpan? timeout)
    {
        _tag = tag;
        _after = after;
        _timeout = timeout ?? DefaultTimeout;
        // Only the whole tag matches, so users.load does not hold users.load_all.
        _tagInSql = new Regex($@"(?<![\w.]){Regex.Escape(tag)}(?![\w.])");
    }

    /// <summary>Holds the tagged command before it runs.</summary>
    public static PauseAtQuery Before(string tag, TimeSpan? timeout = null) => new(tag, after: false, timeout);

    /// <summary>
    /// Holds the request after the tagged command ran and its reader is done, so the rows that it read are loaded. A
    /// rival that commits now changes the rows after the load and before the save.
    /// </summary>
    public static PauseAtQuery After(string tag, TimeSpan? timeout = null) => new(tag, after: true, timeout);

    /// <summary>Waits until the request under test is at the tagged command.</summary>
    public async Task ReachedAsync() => await ReachedConnectionAsync();

    /// <summary>
    /// Waits until <paramref name="rival"/> waits on a lock that the held request has (<c>pg_blocking_pids</c>).
    /// </summary>
    public async Task WaitUntilBlockedAsync(Task rival)
    {
        var held = await ReachedConnectionAsync();
        await using var probe = (NpgsqlConnection)((ICloneable)held).Clone();
        await probe.OpenAsync();
        await using var blocked = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE @held = ANY(pg_blocking_pids(pid)))", probe);
        blocked.Parameters.AddWithValue("held", held.ProcessID);

        var deadline = DateTime.UtcNow + _timeout;
        while (!(bool)(await blocked.ExecuteScalarAsync())!)
        {
            if (rival.IsCompleted)
            {
                await rival;
                throw new InvalidOperationException(
                    $"The rival finished, but it did not wait on a lock of the request held at '{_tag}'.");
            }
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException(
                    $"The rival did not wait on a lock of the request held at '{_tag}' within {_timeout.TotalSeconds} s.");
            await Task.Delay(20);
        }
    }

    /// <summary>Lets the held command go on.</summary>
    public void Release() => _released.TrySetResult();

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (!_after)
            await HoldIfTaggedAsync(command);
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (!_after)
            await HoldIfTaggedAsync(command);
        return result;
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        if (!_after)
            await HoldIfTaggedAsync(command);
        return result;
    }

    public override async ValueTask<InterceptionResult> DataReaderClosingAsync(DbCommand command,
        DataReaderClosingEventData eventData, InterceptionResult result)
    {
        if (_after)
            await HoldIfTaggedAsync(command);
        return result;
    }

    public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        if (_after)
            await HoldIfTaggedAsync(command);
        return result;
    }

    public override async ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        object? result, CancellationToken cancellationToken = default)
    {
        if (_after)
            await HoldIfTaggedAsync(command);
        return result;
    }

    private async Task<NpgsqlConnection> ReachedConnectionAsync()
    {
        try
        {
            return await _reached.Task.WaitAsync(_timeout);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"No command with the tag '{_tag}' ran within {_timeout.TotalSeconds} s.");
        }
    }

    // The hold does not end when the request is canceled, so a test can cancel a held request and then release it. A
    // TimeoutException is transient to the retrying execution strategy, so the hold throws another type.
    private async Task HoldIfTaggedAsync(DbCommand command)
    {
        if (!_tagInSql.IsMatch(command.CommandText) || Interlocked.Exchange(ref _claimed, 1) == 1)
            return;

        _reached.TrySetResult((NpgsqlConnection)command.Connection!);
        try
        {
            await _released.Task.WaitAsync(_timeout);
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException(
                $"The test did not release the command held at '{_tag}' within {_timeout.TotalSeconds} s.");
        }
    }
}
