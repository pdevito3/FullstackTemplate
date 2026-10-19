using System.Reflection;
using Microsoft.AspNetCore.Builder;
using AppTelemetry = FullstackTemplate.Bff.Telemetry;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    // Meters that the instrumentation packages do not add. Polly reports the retries and circuit breaks
    // of the standard resilience handler. The auth meters are new in .NET 10.
    private static readonly string[] FrameworkMeters =
    [
        "Polly",
        "Microsoft.AspNetCore.Authentication",
        "Microsoft.AspNetCore.Authorization",
    ];

    // The OIDC handler answers these paths in middleware, so no route names their spans. Without a
    // name they export as a bare "GET", and the login callback is hard to find among other requests.
    private static readonly string[] OidcCallbackPaths = ["/signin-oidc", "/signout-callback-oidc", "/signout-oidc"];

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.ConfigureSerilog();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var serviceName = builder.Environment.ApplicationName;

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddAttributes(ResourceAttributes(builder)))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(AppTelemetry.Meter.Name)
                    .AddMeter(FrameworkMeters)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(AppTelemetry.Source.Name)
                    .AddSource(serviceName)
                    .AddAspNetCoreInstrumentation(opts =>
                    {
                        opts.Filter = ctx => !IsHealthProbe(ctx.Request.Path);
                        opts.RecordException = true;
                        opts.EnrichWithHttpRequest = (activity, request) =>
                        {
                            if (OidcCallbackPaths.Contains(request.Path.Value))
                            {
                                activity.DisplayName = $"{request.Method} {request.Path.Value}";
                                activity.SetTag("http.route", request.Path.Value);
                            }
                        };
                    })
                    .AddHttpClientInstrumentation(opts => opts.RecordException = true);
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    /// <summary>
    /// Registers Serilog as the logging provider. Console output comes from configuration, and logs go
    /// to OTLP (the Aspire dashboard) when an endpoint is configured.
    /// </summary>
    /// <remarks>
    /// The OTLP sink must live inside this one <c>AddSerilog</c> callback. A logger assigned to
    /// <c>Log.Logger</c> elsewhere is replaced when the host resolves Serilog, so its sinks never run.
    /// </remarks>
    public static TBuilder ConfigureSerilog<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        builder.Services.AddSerilog((services, lc) =>
        {
            lc.ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("Application", builder.Environment.ApplicationName);

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                lc.WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = otlpEndpoint;
                    options.Protocol = OtlpProtocol.Grpc;
                    options.ResourceAttributes = ResourceAttributes(builder);
                });
        });

        return builder;
    }

    /// <summary>
    /// The resource attributes for traces, metrics, and logs. Serilog exports logs on its own, not
    /// through the OpenTelemetry SDK, so both read this one source. Then a filter on version or
    /// environment finds all three signals.
    /// </summary>
    private static Dictionary<string, object> ResourceAttributes(IHostApplicationBuilder builder) => new()
    {
        ["service.name"] = builder.Environment.ApplicationName,
        ["service.version"] = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0",
        ["service.instance.id"] = Environment.MachineName,
        ["deployment.environment"] = builder.Environment.EnvironmentName,
        ["host.name"] = Environment.MachineName,
    };

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            // Local dev / Aspire: OTLP endpoint is set automatically
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Production exporters - uncomment and configure as needed:
        // Azure Monitor: requires Azure.Monitor.OpenTelemetry.AspNetCore package
        // if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        // {
        //     builder.Services.AddOpenTelemetry().UseAzureMonitor();
        // }

        // AWS X-Ray: requires AWS.Distro.OpenTelemetry.AspNetCore package
        // builder.Services.AddOpenTelemetry().UseXRayExporter();

        // Jaeger: requires OpenTelemetry.Exporter.Jaeger package
        // builder.Services.AddOpenTelemetry().WithTracing(t => t.AddJaegerExporter());

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// True for the health and aliveness probes. The orchestrator calls them every few seconds, so
    /// their spans and request logs are noise.
    /// </summary>
    public static bool IsHealthProbe(PathString path) =>
        path.StartsWithSegments(HealthEndpointPath) || path.StartsWithSegments(AlivenessEndpointPath);

    /// <summary>
    /// The request log level: Error for a failure, Verbose for a health probe, and Information for
    /// all other requests. Verbose is below the minimum level, so a healthy probe writes no log.
    /// </summary>
    public static LogEventLevel RequestLogLevel(HttpContext httpContext, double _, Exception? exception) =>
        (exception, httpContext.Response.StatusCode) switch
        {
            (not null, _) or (_, > 499) => LogEventLevel.Error,
            _ when IsHealthProbe(httpContext.Request.Path) => LogEventLevel.Verbose,
            _ => LogEventLevel.Information,
        };

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(HealthEndpointPath);
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }
}
