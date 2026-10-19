using System.Globalization;
using Duende.Bff;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Yarp;
using FullstackTemplate.Bff;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Serilog.Events;

// Bootstrap logger to catch startup errors before host is built
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting FullstackTemplate.Bff");

    var builder = WebApplication.CreateBuilder(args);

    builder.AddServiceDefaults(); // OpenTelemetry, Serilog, health checks, resilience, service discovery
    builder.Services.AddProblemDetails();

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            var frontendUrl = builder.Configuration["services:webfrontend:https:0"]
                ?? builder.Configuration["services:webfrontend:http:0"]
                ?? "http://localhost:5173";

            policy.WithOrigins(frontendUrl.TrimEnd('/'))
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });

    builder.Services.AddBffAuthentication(builder.Configuration, builder.Environment);

    // Configure forwarded headers for reverse proxy (Northflank, AWS, Azure, etc.)
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        // Trust all proxies in production if, for example, Northflank, AWS, Azure, etc. handles the external TLS
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    var app = builder.Build();

    // Must be first to properly set scheme/host from reverse proxy headers
    app.UseForwardedHeaders();

    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = Extensions.RequestLogLevel;
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    app.UseExceptionHandler();

    // In development the Vite dev server serves the SPA. Otherwise the SPA build is in wwwroot.
    var frontendUrl = builder.Configuration["services:webfrontend:https:0"]
                      ?? builder.Configuration["services:webfrontend:http:0"];

    // The SPA fallback skips paths with a file extension, so the JS and CSS bundles need this
    if (string.IsNullOrEmpty(frontendUrl))
    {
        app.UseStaticFiles();
    }

    app.UseCors();
    app.UseAuthentication();
    app.UseRouting();
    app.UseBff();
    app.UseAuthorization();

    app.MapGet("/local-api/hello", () => new { message = "Hello from BFF!", timestamp = DateTime.UtcNow })
        .AsBffApiEndpoint();

    var serverUrl = builder.Configuration["services:server:https:0"]
        ?? builder.Configuration["services:server:http:0"]
        ?? "https://localhost:5001";

    // BFF strips the local path prefix when forwarding, so include /api in the remote URL
    var apiBaseUrl = new Uri(new Uri(serverUrl.TrimEnd('/') + "/"), "api");
    app.MapRemoteBffApiEndpoint("/api", apiBaseUrl)
        .WithAccessToken(RequiredTokenType.UserOrNone);

    app.MapDefaultEndpoints();

    if (!string.IsNullOrEmpty(frontendUrl))
    {
        app.MapGet("/", () => Results.Redirect(frontendUrl));
    }
    else
    {
        // SPA fallback for client-side routing in production
        app.MapFallbackToFile("index.html");
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
