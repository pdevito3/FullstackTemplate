namespace FullstackTemplate.IntegrationTests.Databases;

using System.Collections.Concurrent;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

public sealed class MigrationHostedServiceTests : IAsyncDisposable
{
    private readonly PostgresTestDatabase _database = new();

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Test]
    public async Task startup_migration_on_a_missing_database_creates_it_and_logs_no_error()
    {
        // Arrange
        await _database.StartAsync(createDatabase: false);
        var logs = new ErrorLogs();
        await using var services = TestFixture.BuildServiceProvider(
            s => s.AddLogging(logging => logging.AddProvider(logs)), _database.ConnectionString);
        var migrator = new MigrationHostedService<AppDbContext>(
            services.GetRequiredService<IServiceScopeFactory>(), services.GetRequiredService<IConfiguration>());

        // Act
        await migrator.StartAsync(CancellationToken.None);

        // Assert
        logs.Errors.ShouldBeEmpty();
        await using var scope = services.CreateAsyncScope();
        var pending = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetPendingMigrationsAsync();
        pending.ShouldBeEmpty();
    }

    private sealed class ErrorLogs : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _errors = new();

        public IReadOnlyCollection<string> Errors => _errors;

        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _errors);

        public void Dispose()
        {
        }

        private sealed class Logger(string category, ConcurrentQueue<string> errors) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel))
                    errors.Enqueue($"{category} {eventId.Name}: {formatter(state, exception)}");
            }
        }
    }
}
