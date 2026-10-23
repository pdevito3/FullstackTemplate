namespace FullstackTemplate.Server.Databases;

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal sealed class HandlerWriteGuard : DbCommandInterceptor, IDbTransactionInterceptor
{
    private const string TransactionRefusal =
        "A domain event handler must not begin or use a transaction. " +
        "The save that runs the handler commits its changes in one transaction.";

    public static HandlerWriteGuard Instance { get; } = new();

    private HandlerWriteGuard()
    {
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        RefuseCommand(eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        RefuseCommand(eventData);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        RefuseCommand(eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        RefuseCommand(eventData);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        RefuseCommand(eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        RefuseCommand(eventData);
        return ValueTask.FromResult(result);
    }

    public InterceptionResult<DbTransaction> TransactionStarting(
        DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result)
    {
        RefuseTransaction(eventData.Context);
        return result;
    }

    public ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
        DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result,
        CancellationToken cancellationToken = default)
    {
        RefuseTransaction(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public DbTransaction TransactionUsed(
        DbConnection connection, TransactionEventData eventData, DbTransaction result)
    {
        RefuseTransaction(eventData.Context);
        return result;
    }

    public ValueTask<DbTransaction> TransactionUsedAsync(
        DbConnection connection, TransactionEventData eventData, DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        RefuseTransaction(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void RefuseCommand(CommandEventData eventData)
    {
        if (eventData is { Context: AppDbContext { IsRunningDomainEventHandlers: true } }
            && eventData.CommandSource != CommandSource.LinqQuery)
            throw new InvalidOperationException(
                "A domain event handler must not run SQL other than a LINQ query, such as ExecuteUpdateAsync, " +
                "ExecuteDeleteAsync, FromSql, SqlQuery, or ExecuteSql. Read with LINQ, and change the entities " +
                "through the dbContext of Handle, so that the save writes them.");
    }

    private static void RefuseTransaction(DbContext? context)
    {
        if (context is AppDbContext { IsRunningDomainEventHandlers: true })
            throw new InvalidOperationException(TransactionRefusal);
    }
}
