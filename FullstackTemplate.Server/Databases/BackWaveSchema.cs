namespace FullstackTemplate.Server.Databases;

using System.Diagnostics.CodeAnalysis;
using BackWave.Postgres;
using Npgsql;

/// <summary>The BackWave job store, in its own schema of appdb, and the migration of that schema.</summary>
internal static class BackWaveSchema
{
    public const string SchemaName = "backwave";

    /// <exception cref="InvalidOperationException">The configuration has no appdb connection string.</exception>
    public static PostgresJobStore CreateStore(IConfiguration configuration, ILoggerFactory loggerFactory)
        => new(new PostgresStoreOptions
        {
            ConnectionString = configuration.GetConnectionString(DatabaseConsts.DatabaseName) is { Length: > 0 } connectionString
                ? connectionString
                : throw new InvalidOperationException(
                    $"The job store needs the connection string ConnectionStrings:{DatabaseConsts.DatabaseName}."),
            // Only MigrationHostedService changes the schema, so a first enqueue in a commit does not migrate.
            AutoMigrate = false,
            SchemaName = SchemaName,
            LoggerFactory = loggerFactory,
        });

    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
        Justification = "The BackWave migrator writes only the backwave schema, which has no audit.")]
    public static async Task MigrateAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await PostgresMigrator.MigrateAsync(dataSource, SchemaName, coordinate: true, cancellationToken);
    }
}
