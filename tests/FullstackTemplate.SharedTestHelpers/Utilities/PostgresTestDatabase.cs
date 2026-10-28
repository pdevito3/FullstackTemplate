namespace FullstackTemplate.SharedTestHelpers.Utilities;

using FullstackTemplate.AppHost;
using Npgsql;
using Testcontainers.PostgreSql;

/// <summary>
/// Gives a test run its own empty database on a shared Postgres container.
/// </summary>
/// <remarks>
/// Local runs reuse one container, so they skip the container startup time.
/// Each run creates a new database, so data from earlier runs, other branches, and other
/// test projects never collides with this run. CI sets the <c>CI</c> variable and gets
/// a new container for each run.
/// </remarks>
public sealed class PostgresTestDatabase : IAsyncDisposable
{
    private static readonly bool ReuseContainer = Environment.GetEnvironmentVariable("CI") is null;

    // A pool grows with the number of tests that run at once. The Npgsql default of 100 let one test run use all the
    // connections of a default Postgres.
    public const int MaxPoolSize = 20;

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder($"postgres:{Config.PostgresImageTag}")
        .WithReuse(ReuseContainer)
        // The test runs of all worktrees on the machine share this container
        .WithCommand("-c", "max_connections=400")
        .WithLabel("fullstacktemplate.reuse", "test-postgres")
        .Build();

    private readonly string _databaseName = $"test_{Guid.NewGuid():N}";

    /// <summary>The connection string for the empty database of this run.</summary>
    public string ConnectionString { get; private set; } = null!;

    public async Task StartAsync(bool createDatabase = true)
    {
        // Testcontainers finds a reusable container by its hash, but it does not lock across processes. The test
        // projects start at the same time, so each one can create a container with the same hash. The next run then
        // fails with "Sequence contains more than one element". The lock lets one process at a time start it.
        await using (await AcquireStartLockAsync())
        {
            await _container.StartAsync();
        }
        if (createDatabase)
            await ExecuteOnServerAsync($"""CREATE DATABASE "{_databaseName}" """);
        ConnectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = _databaseName,
            MaxPoolSize = MaxPoolSize
        }.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        if (!ReuseContainer)
        {
            await _container.DisposeAsync();
            return;
        }

        // The container stays up for the next run. Drop only the database of this run.
        await ExecuteOnServerAsync($"""DROP DATABASE IF EXISTS "{_databaseName}" WITH (FORCE)""");
    }

    private static async Task<FileStream> AcquireStartLockAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), "fullstacktemplate-test-postgres.lock");
        var deadline = DateTime.UtcNow.AddMinutes(5);
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }
        }
    }

    private async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
