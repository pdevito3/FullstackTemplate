namespace FullstackTemplate.Server.Resources.Extensions;

using System.Diagnostics;
using Databases;
using Domain.Users;
using Exceptions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using Services;
using ZiggyCreatures.Caching.Fusion;

public static class WebAppServiceConfiguration
{
    public static void ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = DomainProblemDetails.Customize);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddFusionCache()
            .WithDefaultEntryOptions(options =>
            {
                options.Duration = TimeSpan.FromMinutes(5);
                options.IsFailSafeEnabled = true;
                options.FailSafeMaxDuration = TimeSpan.FromHours(2);
                options.FailSafeThrottleDuration = TimeSpan.FromSeconds(30);
            });
        builder.Services.Configure<TenancyOptions>(builder.Configuration.GetSection(TenancyOptions.SectionName));
        builder.Services.AddApplicationServices();
        builder.Services.AddScoped<SystemTenantAccess>();
        builder.Services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
            // Outermost behavior, so its span covers every later behavior and the handler itself.
            cfg.AddOpenBehavior(typeof(RequestTracingBehavior<,>));
            // Runs only for requests that have IRequireTenantAdmin, because of its generic constraint.
            cfg.AddOpenBehavior(typeof(TenantAdminBehavior<,>));
        });
        builder.Services.AddApiVersioningExtension();
        builder.Services.AddControllers(options =>
        {
            options.Filters.Add<PaginationHeaderFilter>();
            // The keys of the validation errors use the JSON names of the request, for example "name", not "Name"
            options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
        });
        builder.Services.AddSwaggerExtension(builder.Configuration);
        builder.Services.AddJwtBearerAuthentication(builder.Configuration, builder.Environment);

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
                .UseSnakeCaseNamingConvention();
        });
        builder.EnrichNpgsqlDbContext<AppDbContext>();
    }
}
