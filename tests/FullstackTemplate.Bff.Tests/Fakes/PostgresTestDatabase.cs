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

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithReuse(ReuseContainer)
        .WithLabel("fullstacktemplate.reuse", "test-postgres")
        .Build();

    private readonly string _databaseName = $"bff_test_{Guid.NewGuid():N}";

    /// <summary>The connection string for the empty database of this run.</summary>
    public string ConnectionString { get; private set; } = null!;

    public async Task StartAsync()
    {
        await _container.StartAsync();
        await ExecuteOnServerAsync($"""CREATE DATABASE "{_databaseName}" """);
        ConnectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = _databaseName
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

    private async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
