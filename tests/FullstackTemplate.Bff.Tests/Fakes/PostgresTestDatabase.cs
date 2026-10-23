namespace FullstackTemplate.Bff.Tests.Fakes;

using Npgsql;
using Testcontainers.PostgreSql;

/// <summary>
/// Gives a test run its own empty <c>bffdb</c> on a shared Postgres container.
/// </summary>
/// <remarks>
/// The same rules as <c>PostgresTestDatabase</c> of the shared test helpers, which this project cannot reference,
/// because that project references the server. Local runs reuse one container and create a new database for
/// each run. CI sets the <c>CI</c> variable and gets a new container for each run.
/// </remarks>
public sealed class PostgresTestDatabase : IAsyncDisposable
{
    private static readonly bool ReuseContainer = Environment.GetEnvironmentVariable("CI") is null;

    private const int MaxPoolSize = 20;

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithReuse(ReuseContainer)
        .WithCommand("-c", "max_connections=400")
        .WithLabel("fullstacktemplate.reuse", "test-postgres")
        .Build();

    private readonly string _databaseName = $"bff_test_{Guid.NewGuid():N}";

    /// <summary>The connection string for the empty database of this run.</summary>
    public string ConnectionString { get; private set; } = null!;

    public async Task StartAsync()
    {
        // Testcontainers finds a reusable container by its hash, but it does not lock across processes. The test
        // projects start at the same time, so each one can create a container with the same hash. The next run then
        // fails with "Sequence contains more than one element". The lock lets one process at a time start it.
        await using (await AcquireStartLockAsync())
        {
            await _container.StartAsync();
        }
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
