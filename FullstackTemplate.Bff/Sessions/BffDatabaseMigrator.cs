using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Sessions;

/// <summary>
/// Applies the migrations of <c>bffdb</c> at startup, before the Data Protection key ring loads and before the
/// server takes requests. EF Core takes a database lock for the migration, so many BFF instances can start at
/// the same time.
/// </summary>
public sealed class BffDatabaseMigrator(IServiceScopeFactory scopeFactory) : IHostedService
{
    private static readonly ILogger Logger = Log.ForContext<BffDatabaseMigrator>();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Source.StartActivity("database.migrate");
        activity?.SetTag("app.db_context", nameof(BffDbContext));
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BffDbContext>();
            // The Npgsql MigrateAsync reads the history table before it creates the database and the table, and EF
            // Core logs the failed read as an error. Create both first.
            var databaseCreator = dbContext.GetService<IRelationalDatabaseCreator>();
            if (!await databaseCreator.ExistsAsync(cancellationToken))
            {
                await databaseCreator.CreateAsync(cancellationToken);
                Logger.Here().Information("Created the database for {DbContext}", nameof(BffDbContext));
            }
            await dbContext.GetService<IHistoryRepository>().CreateIfNotExistsAsync(cancellationToken);
            await dbContext.Database.MigrateAsync(cancellationToken);
            Logger.Here().Information("Migrations complete for {DbContext}", nameof(BffDbContext));
        }
        catch (Exception ex)
        {
            Telemetry.RecordError(activity, ex);
            Logger.Here().Error(ex, "Could not apply the migrations of {DbContext}", nameof(BffDbContext));
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
