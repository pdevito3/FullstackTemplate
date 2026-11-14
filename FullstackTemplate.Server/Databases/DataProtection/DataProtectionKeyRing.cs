namespace FullstackTemplate.Server.Databases.DataProtection;

using System.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

public static class DataProtectionKeyRing
{
    /// <summary>
    /// The application name of Data Protection. Each Server process has it, so that each process can read the
    /// tokens of the others.
    /// </summary>
    public const string ApplicationName = "FullstackTemplate.Server";

    /// <summary>
    /// Adds Data Protection with its key ring in <see cref="DataProtectionKeysDbContext"/>. Each role adds it, so that
    /// each role migrates the same schemas of appdb.
    /// </summary>
    public static void AddDataProtectionKeyRing(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<DataProtectionKeysDbContext>((provider, options) => ConfigureKeysDb(options,
            provider.GetRequiredService<IConfiguration>().GetConnectionString(DatabaseConsts.DatabaseName)));
        // The retries and the Npgsql traces and metrics. The check of AppDbContext covers appdb in /health.
        builder.EnrichNpgsqlDbContext<DataProtectionKeysDbContext>(settings => settings.DisableHealthChecks = true);

        builder.Services.AddDataProtection()
            .SetApplicationName(ApplicationName)
            .PersistKeysToDbContext<DataProtectionKeysDbContext>();
    }

    /// <summary>The Npgsql options of <see cref="DataProtectionKeysDbContext"/>, for the app and for the EF Core tools.</summary>
    public static void ConfigureKeysDb(DbContextOptionsBuilder options, string? connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsHistoryTable(HistoryRepository.DefaultTableName, DataProtectionKeysDbContext.Schema)
                .ConfigureDataSource(dataSource => dataSource.ConfigureTracing(tracing => tracing
                    // The key ring loads at startup and on a timer, outside a request. With no recorded activity,
                    // each query would export as a trace of its own.
                    .ConfigureCommandFilter(_ => Activity.Current is { IsAllDataRequested: true }))))
            .UseSnakeCaseNamingConvention();
}
