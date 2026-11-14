namespace FullstackTemplate.Server.Resources.Extensions;

using System.Diagnostics;
using Databases;
using Databases.DataProtection;
using Domain.Attachments;
using Domain.Tenants;
using Domain.Users;
using Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Storage;
using ZiggyCreatures.Caching.Fusion;

public static class WebAppServiceConfiguration
{
    /// <summary>
    /// Adds the core services: the database, the Data Protection key ring, the background jobs, the domain event handlers, MediatR, the caches, and the tenant scope. It adds no transport.
    /// The integration tests call only this method. <c>AddEntrypoints</c> adds HTTP, gRPC, and MCP.
    /// The core has no caller until <c>AddEntrypoints</c> replaces <see cref="NoPrincipalAccessor"/>. The worker groups
    /// that run the jobs are added only when the <paramref name="role"/> runs jobs.
    /// </summary>
    public static void ConfigureServices(this WebApplicationBuilder builder, ServerRole role)
    {
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddFusionCache()
            .WithDefaultEntryOptions(options =>
            {
                options.Duration = TimeSpan.FromMinutes(5);
                options.IsFailSafeEnabled = true;
                options.FailSafeMaxDuration = TimeSpan.FromHours(2);
                options.FailSafeThrottleDuration = TimeSpan.FromSeconds(30);
            });
        builder.Services.Configure<TenancyOptions>(builder.Configuration.GetSection(TenancyOptions.SectionName));
        builder.Services.AddSingleton(Options.Create(FirstPartyClientOptions.FromConfiguration(builder.Configuration)));
        builder.Services.AddApplicationServices();
        builder.Services.AddSingleton<ICurrentPrincipalAccessor, NoPrincipalAccessor>();
        builder.Services.AddScoped<ScopeGrant>();
        builder.AddObjectStorage();
        builder.Services.AddScoped<AttachmentStorage>();
        builder.Services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
            // Outermost behavior, so its span covers every later behavior and the handler itself.
            cfg.AddOpenBehavior(typeof(RequestTracingBehavior<,>));
            cfg.AddOpenBehavior(typeof(CallerIdentityBehavior<,>));
            cfg.AddOpenBehavior(typeof(RequestValidationBehavior<,>));
            // Only for an IRunsAgainWhenRaced request. Each run again runs AuditRequestBehavior and the handler again.
            cfg.AddOpenBehavior(typeof(RaceRetryBehavior<,>));
            // Innermost behavior, next to the handler whose saves it names.
            cfg.AddOpenBehavior(typeof(AuditRequestBehavior<,>));
        });

        builder.Services.AddSingleton<TenantLookupInterceptor>();
        builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var connectionString = builder.Configuration.GetConnectionString(DatabaseConsts.DatabaseName);

            options.UseNpgsql(connectionString, npgsql => npgsql.ConfigureDataSource(dataSource =>
                    dataSource.ConfigureTracing(tracing => tracing
                        // A command with no recorded ambient activity has no exported request or TraceRoot
                        // to belong to, and would export as an orphan. This covers no activity at all and
                        // an activity that instrumentation filtered out, such as the /health request.
                        .ConfigureCommandFilter(_ => Activity.Current is { IsAllDataRequested: true })
                        .ConfigureCommandEnrichmentCallback((activity, command) =>
                            QueryTagEnrichment.Enrich(activity, command.CommandText)))))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(serviceProvider.GetRequiredService<TenantLookupInterceptor>());
        });
        builder.EnrichNpgsqlDbContext<AppDbContext>();
        builder.AddDataProtectionKeyRing();
        builder.Services.AddBackgroundJobs(builder.Configuration, role);

        builder.Services.AddDomainEvents(typeof(Program).Assembly);
    }
}
