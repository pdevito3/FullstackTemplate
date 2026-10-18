namespace FullstackTemplate.Server.Resources.Extensions;

using System.Diagnostics;
using System.Text.Json.Serialization;
using Databases;
using Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
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
        builder.Services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
            // Outermost behavior, so its span covers every later behavior and the handler itself.
            cfg.AddOpenBehavior(typeof(RequestTracingBehavior<,>));
        });
        builder.Services.AddApiVersioningExtension();
        // JSON only, so each operation in the OpenAPI document has one media type.
        builder.Services.AddControllers(options =>
            {
                options.Filters.Add(new ProducesAttribute("application/json"));
                var jsonInput = options.InputFormatters.OfType<SystemTextJsonInputFormatter>().Single();
                jsonInput.SupportedMediaTypes.Clear();
                jsonInput.SupportedMediaTypes.Add("application/json");
            })
            .AddJsonOptions(options => options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict);
        // The web default also reads numbers from strings, so the OpenAPI document typed each number as a number or a string.
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
        builder.Services.AddSwaggerExtension(builder.Configuration);
        builder.Services.AddJwtBearerAuthentication(builder.Configuration, builder.Environment);

        builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            // use this to account for malformed DATABASE_URL from Northflank. Please remove it if you don't need it
            var connectionString = ConnectionStringHelper
                .ConvertPostgresUri(builder.Configuration.GetConnectionString(DatabaseConsts.DatabaseName));
            // var connectionString = builder.Configuration.GetConnectionString(DatabaseConsts.DatabaseName);
            
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
