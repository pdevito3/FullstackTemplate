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
using Serilog.Sinks.OpenTelemetry;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

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
        var serviceVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: serviceName,
                    serviceVersion: serviceVersion,
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes([
                    new("deployment.environment", builder.Environment.EnvironmentName),
                    new("host.name", Environment.MachineName)
                ]))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(AppTelemetry.Meter.Name)
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
                        opts.Filter = ctx =>
                            !ctx.Request.Path.StartsWithSegments(HealthEndpointPath) &&
                            !ctx.Request.Path.StartsWithSegments(AlivenessEndpointPath);
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
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = builder.Environment.ApplicationName
                    };
                });
        });

        return builder;
    }

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
