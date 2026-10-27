namespace FullstackTemplate.Server.Databases;

using System.Net.Sockets;
using Medallion.Threading.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Resources;
using Serilog;
using Serilog.Core;

public class MigrationHostedService<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration)
    : IHostedService
    where TDbContext : DbContext
{
    // ForContext<T> names a generic type with its assembly-qualified arguments, which fills a console line.
    private readonly ILogger _logger = Log.ForContext(
        Constants.SourceContextPropertyName, $"{typeof(MigrationHostedService<>).Namespace}.MigrationHostedService");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Startup has no request to hang this work on. Without a root, each migration query exports as
        // its own rootless trace. With one, the whole run is a single trace named after what it does.
        using var trace = Telemetry.TraceRoot(
            [("app.db_context", typeof(TDbContext).Name)],
            "database.migrate");

        try
        {
            _logger.Here().Information("Applying migrations for {DbContext}", typeof(TDbContext).Name);

            var connectionString = configuration.GetConnectionString(DatabaseConsts.DatabaseName);

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.Here().Warning(
                    "No connection string found for {DatabaseName}. Skipping migrations",
                    DatabaseConsts.DatabaseName);
                return;
            }

            var distributedLock = new PostgresDistributedLock(
                new PostgresAdvisoryLockKey($"{DatabaseConsts.DatabaseName}_MigrationLock", allowHashing: true),
                connectionString);

            await using (await distributedLock.AcquireAsync(cancellationToken: cancellationToken))
            {
                // Another instance can hold the lock for a full migration run. The event shows where
                // the wait ended, so the span separates time spent waiting from time spent migrating.
                Telemetry.AddEvent([], "migration.lock_acquired");
                _logger.Here().Information("Starting database migration...");

                await using var scope = scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
                // On a new database, MigrateAsync reads the history table before it creates it, and EF Core logs the
                // failed read as an error. Create the table first.
                if (await context.GetService<IRelationalDatabaseCreator>().ExistsAsync(cancellationToken))
                    await context.GetService<IHistoryRepository>().CreateIfNotExistsAsync(cancellationToken);
                await context.Database.MigrateAsync(cancellationToken);
            }

            _logger.Here().Information("Migrations complete for {DbContext}", typeof(TDbContext).Name);
        }
        catch (Exception ex) when (ex is SocketException or NpgsqlException)
        {
            Telemetry.RecordError(trace.Activity, ex);
            _logger.Here().Error(
                ex,
                "Could not connect to the database -- please check the connection string and make sure the database is running");
            throw;
        }
        catch (Exception ex)
        {
            Telemetry.RecordError(trace.Activity, ex);
            _logger.Here().Error(ex, "An error occurred while applying the database migrations.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
