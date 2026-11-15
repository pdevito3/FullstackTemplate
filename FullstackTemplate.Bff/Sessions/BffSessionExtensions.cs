using System.Diagnostics;
using FullstackTemplate.Bff.Authentication;
using FullstackTemplate.Bff.BackChannel;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FullstackTemplate.Bff.Sessions;

public static class BffSessionExtensions
{
    /// <summary>
    /// The application name of Data Protection. All BFF instances share it, so that each instance can read the
    /// cookies and the tickets of the others.
    /// </summary>
    public const string DataProtectionApplicationName = "FullstackTemplate.Bff";

    /// <summary>
    /// The SQLSTATE <c>idle_in_transaction_session_timeout</c>. Npgsql has no constant for it.
    /// </summary>
    private const string IdleInTransactionSessionTimeout = "25P03";

    /// <summary>
    /// Adds <c>bffdb</c>, the BFF session store, the Data Protection keys in <c>bffdb</c>, the session store of the
    /// cookie handler, the replay cache of the logout tokens, and the cleanup. Call it before
    /// <c>AddBffAuthentication</c>, so that the migrations run before the Data Protection key ring loads.
    /// </summary>
    public static IHostApplicationBuilder AddBffSessions(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddOptions<BffSessionOptions>()
            .BindConfiguration(BffSessionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<BffDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString(BffDbContext.DatabaseName)
                ?? throw new InvalidOperationException($"The connection string '{BffDbContext.DatabaseName}' is required.");
            ConfigureBffDb(options, connectionString);
            // At idle_in_transaction_session_timeout, Postgres ends the session and rolls its transaction back. The
            // next command gets 25P03, which Npgsql does not treat as transient. A retry is safe, as for a lost connection.
            options.UseNpgsql(npgsql => npgsql.EnableRetryOnFailure([IdleInTransactionSessionTimeout]));
        });
        // Aspire keeps an NpgsqlRetryingExecutionStrategy that the options already set, so the 25P03 retry stays
        builder.EnrichNpgsqlDbContext<BffDbContext>();

        // First, so that the migrations run before the other hosted services, such as the Data Protection key ring
        services.AddHostedService<BffDatabaseMigrator>();

        services.AddDataProtection()
            .SetApplicationName(DataProtectionApplicationName)
            .PersistKeysToDbContext<BffDbContext>();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IBffSessionStore, EfBffSessionStore>();
        services.AddScoped<BffSessionRequest>();
        services.AddScoped<RefreshTokenRevocation>();
        services.AddScoped<LogoutTokenReplayCache>();
        services.AddSingleton<BffTicketStore>();

        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<BffTicketStore, IOptions<BffSessionOptions>>((cookie, ticketStore, sessionOptions) =>
            {
                cookie.SessionStore = ticketStore;
                cookie.ExpireTimeSpan = sessionOptions.Value.SlidingExpiration;
                cookie.SlidingExpiration = true;
            });

        services.AddSingleton<BffSessionCleanupService>();
        services.AddHostedService(sp => sp.GetRequiredService<BffSessionCleanupService>());

        return builder;
    }

    /// <summary>
    /// The Npgsql options of <c>bffdb</c>, for the app and for the EF Core tools.
    /// </summary>
    public static void ConfigureBffDb(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.ConfigureDataSource(dataSource =>
                dataSource.ConfigureTracing(tracing => tracing
                    // A command with no recorded activity, such as a query of the /health request, would export
                    // as a trace of its own
                    .ConfigureCommandFilter(_ => Activity.Current is { IsAllDataRequested: true }))))
            .UseSnakeCaseNamingConvention();
}
