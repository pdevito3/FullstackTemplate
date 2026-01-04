namespace FullstackTemplate.Server.Databases;

using System.Net.Sockets;
using Medallion.Threading.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Serilog;

public class MigrationHostedService<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration)
    : IHostedService
    where TDbContext : DbContext
{
    private readonly ILogger _logger = Log.ForContext<MigrationHostedService<TDbContext>>();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.Information("Applying migrations for {DbContext}", typeof(TDbContext).Name);

            // use this to account for malformed DATABASE_URL from Northflank. Please remove it if you don't need it
            var connectionString = ConnectionStringHelper
                .ConvertPostgresUri(configuration.GetConnectionString(DatabaseConsts.DatabaseName));
            // var connectionString = configuration.GetConnectionString(DatabaseConsts.DatabaseName);
            
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.Warning(
                    "No connection string found for {DatabaseName}. Skipping migrations",
                    DatabaseConsts.DatabaseName);
                return;
            }

            var distributedLock = new PostgresDistributedLock(
                new PostgresAdvisoryLockKey($"{DatabaseConsts.DatabaseName}_MigrationLock", allowHashing: true),
                connectionString);

            await using (await distributedLock.AcquireAsync(cancellationToken: cancellationToken))
            {
                _logger.Information("Starting database migration...");

                await using var scope = scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
                await context.Database.MigrateAsync(cancellationToken);
            }

            _logger.Information("Migrations complete for {DbContext}", typeof(TDbContext).Name);
        }
        catch (Exception ex) when (ex is SocketException or NpgsqlException)
        {
            _logger.Error(
                ex,
                "Could not connect to the database -- please check the connection string and make sure the database is running");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An error occurred while applying the database migrations.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
