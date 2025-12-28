namespace FullstackTemplate.Server.Resources.Extensions;

using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

public static class HostExtensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.ConfigureSerilogOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    private static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
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
                    .AddMeter(Telemetry.Meter.Name)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(Telemetry.Source.Name)
                    .AddSource(serviceName)
                    .AddAspNetCoreInstrumentation(opts =>
                    {
                        opts.Filter = ctx =>
                            !ctx.Request.Path.StartsWithSegments(HealthEndpointPath) &&
                            !ctx.Request.Path.StartsWithSegments(AlivenessEndpointPath);
                        opts.RecordException = true;
                    })
                    .AddHttpClientInstrumentation(opts => opts.RecordException = true);
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder ConfigureSerilogOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
                .WriteTo.Console()
                .WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = otlpEndpoint;
                    options.Protocol = OtlpProtocol.Grpc;
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = builder.Environment.ApplicationName
                    };
                })
                .CreateLogger();
        }

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

    private static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Map health endpoints in Development and Testing environments
        if (app.Environment.IsDevelopment() ||
            app.Environment.EnvironmentName.Contains("Testing", StringComparison.OrdinalIgnoreCase))
        {
            app.MapHealthChecks(HealthEndpointPath);
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
