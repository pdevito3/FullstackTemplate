namespace FullstackTemplate.Bff.Tests.Integration;

using System.Globalization;
using FullstackTemplate.Bff.Sessions;
using FullstackTemplate.Bff.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;
using Shouldly;

public class BffDatabaseMigratorTests
{
    [Test]
    public async Task startup_migration_on_a_missing_database_creates_it_and_logs_no_error()
    {
        await using var database = new PostgresTestDatabase();
        await database.StartAsync(createDatabase: false);
        var logs = new CapturedLogs();

        await using (var app = BffApp.StartAnotherInstance(
            new Dictionary<string, string?> { [$"ConnectionStrings:{BffDbContext.DatabaseName}"] = database.ConnectionString },
            services => services.AddSingleton<ILogEventSink>(logs)))
        {
            await using var scope = app.Services.CreateAsyncScope();
            var pending = await scope.ServiceProvider.GetRequiredService<BffDbContext>().Database.GetPendingMigrationsAsync();
            pending.ShouldBeEmpty();
        }

        // The sink also gets the events of BFF hosts that other tests run at the same time, so keep only the migration sources.
        logs.AtLeast(LogEventLevel.Error)
            .Where(e => e.Properties.GetValueOrDefault("SourceContext")?.ToString().Trim('"') is { } source
                && (source.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                    || source.StartsWith("Npgsql", StringComparison.Ordinal)
                    || source.StartsWith("FullstackTemplate.Bff.Sessions", StringComparison.Ordinal)))
            .Select(e => $"{e.Properties.GetValueOrDefault("SourceContext")}: {e.RenderMessage(CultureInfo.InvariantCulture)}").ShouldBeEmpty();
    }
}
