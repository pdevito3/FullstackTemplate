namespace FullstackTemplate.Server.Databases;

using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Resources;
using Serilog;

/// <summary>
/// A Postgres session advisory lock on one aggregate root, for a rule that reads rows that the command does not write.
/// A write that adds to the set of the root takes the shared lock. A write that needs the set to stay as it read it
/// takes the exclusive lock. See <c>docs/backend/concurrency.md</c>, section "Lock".
/// </summary>
public sealed class RootLock
{
    /// <summary>A tenant delete takes the exclusive lock, so no write that takes the shared lock can add a row to the tenant while the delete runs.</summary>
    public static readonly RootLock Tenant = new(1, "tenants");

    private static readonly ILogger Logger = Log.ForContext<RootLock>();

    // The first key is the kind of the root. The one-key locks of the EF migrations then do not share a key with it.
    private readonly int _kind;
    private readonly string _area;
    private readonly Mode _shared;
    private readonly Mode _exclusive;
    private readonly string _endSession;

    private RootLock(int kind, string area)
    {
        _kind = kind;
        _area = area;
        _shared = new Mode("shared",
            Tagged($"{area}.lock_shared", "pg_advisory_lock_shared({0}, {1})"),
            Tagged($"{area}.unlock_shared", "pg_advisory_unlock_shared({0}, {1})"));
        _exclusive = new Mode("exclusive",
            Tagged($"{area}.lock_exclusive", "pg_advisory_lock({0}, {1})"),
            Tagged($"{area}.unlock_exclusive", "pg_advisory_unlock({0}, {1})"));
        _endSession = Tagged($"{area}.end_session", "pg_terminate_backend(pg_backend_pid())");
    }

    private sealed record Mode(string Name, string TakeSql, string FreeSql);

    /// <summary>Runs <paramref name="work"/> while this request and the other shared holders hold the lock of the root.</summary>
    public Task<T> RunSharedAsync<T>(AppDbContext dbContext, string rootId, Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken)
        => RunAsync(dbContext, rootId, _shared, work, cancellationToken);

    /// <summary>Runs <paramref name="work"/> while only this request holds the lock of the root.</summary>
    public Task<T> RunExclusiveAsync<T>(AppDbContext dbContext, string rootId, Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken)
        => RunAsync(dbContext, rootId, _exclusive, work, cancellationToken);

    // The lock ends with its connection. So the strategy runs the lock, the reads, the checks, and the save again on a
    // new connection after a transient failure, not only the save. After a lost reply of the commit, the save in the
    // work finds its audit entries, so the work does not run again.
    private async Task<T> RunAsync<T>(AppDbContext dbContext, string rootId, Mode mode,
        Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        if (dbContext.ChangeTracker.HasChanges() || dbContext.HasUnsavedActions)
            throw new InvalidOperationException(
                "Take the lock before the first change. A run again after a transient failure starts with an empty tracker, so it would save without the changes.");

        Activity.Current?.SetTag("app.lock.mode", mode.Name);
        var key = KeyOf(rootId);
        var runs = 0;
        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            if (++runs > 1)
            {
                Telemetry.AddEvent([("app.lock.run", runs)], "lock.run_again");
                dbContext.DiscardLostRace();
            }

            await dbContext.Database.OpenConnectionAsync(ct);
            try
            {
                await RunSqlAsync(dbContext, mode.TakeSql, [_kind, key], ct);
                dbContext.InLockedWork = true;
                return await work(ct);
            }
            finally
            {
                dbContext.InLockedWork = false;
                await FreeAsync(dbContext, key, mode);
                await dbContext.Database.CloseConnectionAsync();
            }
        }, cancellationToken);
    }

    // The free runs also after a canceled take, because Postgres can grant the lock before the cancel arrives. A free
    // of a lock that the session does not hold does nothing.
    private async Task FreeAsync(AppDbContext dbContext, int key, Mode mode)
    {
        try
        {
            await RunSqlAsync(dbContext, mode.FreeSql, [_kind, key], CancellationToken.None);
        }
        catch (Exception exception)
        {
            // The pool frees the session locks only when the next request uses the connection, so the lock would stay until then.
            Logger.Here().Warning(exception, "Could not free the {LockMode} lock of {LockArea}, so the session ends",
                mode.Name, _area);
            try
            {
                await RunSqlAsync(dbContext, _endSession, [], CancellationToken.None);
            }
            catch (Exception)
            {
                // The end of the session also ends this command, so it throws. A dead connection has no locks.
            }
        }
    }

    // The hash is in .NET, not hashtext in Postgres, so an ID that Postgres text cannot hold, such as one with a NUL,
    // still gets a lock, and the work gives its usual 404 or 400. Two IDs with the same key only wait on each other.
    private static int KeyOf(string rootId)
        => BinaryPrimitives.ReadInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(rootId)));

    // The tag names the SQL span, as TagWithOperationCallSite does for a LINQ query.
    private static string Tagged(string operationName, string call)
        => $"-- {QueryTagging.QueryPrefix} {operationName}\nSELECT {call}";

    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
        Justification = "The advisory lock functions write no table, so the command needs no audit entry.")]
    private static Task<int> RunSqlAsync(AppDbContext dbContext, string sql, object[] parameters,
        CancellationToken cancellationToken)
        => dbContext.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
}
