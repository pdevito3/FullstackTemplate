namespace FullstackTemplate.Server.Databases;

using System.Net.Sockets;
using DataProtection;
using Medallion.Threading.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Resources;
using Serilog;

/// <summary>
/// Migrates the schemas of appdb: the EF Core schemas of <see cref="AppDbContext"/> and
/// <see cref="DataProtectionKeysDbContext"/>, then the BackWave schema.
/// </summary>
/// <remarks>
/// The host runs <see cref="StartingAsync"/> of each lifecycle service before <c>StartAsync</c> of any service. So the
/// schemas exist before a job pump, the BackWave metrics service, the load of the Data Protection key ring, or Kestrel
/// starts.
/// </remarks>
public sealed class MigrationHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration)
    : IHostedLifecycleService
{
    private readonly ILogger _logger = Log.ForContext<MigrationHostedService>();

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString(DatabaseConsts.DatabaseName);
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.Here().Warning(
                "No connection string found for {DatabaseName}. Skipping migrations",
                DatabaseConsts.DatabaseName);
            return;
        }

        var database = new NpgsqlConnectionStringBuilder(connectionString).Database;
        // Startup has no request to hang this work on. Without a root, each migration query exports as
        // its own rootless trace. With one, the whole run is a single trace named after what it does.
        using var trace = Telemetry.TraceRoot([("db.namespace", database)], "database.migrate");

        try
        {
            _logger.Here().Information("Applying the migrations of {DatabaseName}", database);

            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // The lock connects to the database, so the database must exist before the lock.
            var databaseCreator = context.GetService<IRelationalDatabaseCreator>();
            if (!await databaseCreator.ExistsAsync(cancellationToken))
                await CreateDatabaseAsync(databaseCreator, database, cancellationToken);

            var distributedLock = new PostgresDistributedLock(
                new PostgresAdvisoryLockKey($"{DatabaseConsts.DatabaseName}_MigrationLock", allowHashing: true),
                connectionString);

            await using (await distributedLock.AcquireAsync(cancellationToken: cancellationToken))
            {
                // Another instance can hold the lock for a full migration run. The event shows where
                // the wait ended, so the span separates time spent waiting from time spent migrating.
                Telemetry.AddEvent([], "migration.lock_acquired");
                _logger.Here().Information("Starting database migration...");

                await MigrateAsync(context, cancellationToken);
                await MigrateAsync(scope.ServiceProvider.GetRequiredService<DataProtectionKeysDbContext>(), cancellationToken);
                await BackWaveSchema.MigrateAsync(connectionString, cancellationToken);
            }

            _logger.Here().Information("Migrations complete for {DatabaseName}", database);
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

    private async Task CreateDatabaseAsync(
        IRelationalDatabaseCreator databaseCreator,
        string? database,
        CancellationToken cancellationToken)
    {
        try
        {
            await databaseCreator.CreateAsync(cancellationToken);
            _logger.Here().Information("Created the database {DatabaseName}", database);
        }
        catch (PostgresException e) when (e.SqlState is PostgresErrorCodes.DuplicateDatabase
                                              or PostgresErrorCodes.UniqueViolation)
        {
            // A Server process that starts at the same time can create the database first, for example the worker of
            // a split deploy. Postgres then refuses the second create: with 42P04 when the first create committed
            // before the name check, or with 23505 on the pg_database index when it commits during the second create.
            if (!await databaseCreator.ExistsAsync(cancellationToken))
                throw;
        }
    }

    private static async Task MigrateAsync(DbContext context, CancellationToken cancellationToken)
    {
        // On a new database, MigrateAsync reads the history table before it creates it, and EF Core logs the failed
        // read as an error. Create the table first.
        await context.GetService<IHistoryRepository>().CreateIfNotExistsAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
