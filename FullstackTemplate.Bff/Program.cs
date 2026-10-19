using System.Globalization;
using Duende.Bff;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Yarp;
using FullstackTemplate.Bff;
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

    var app = builder.Build();

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

    // The Vite dev server serves the SPA
    var frontendUrl = builder.Configuration["services:webfrontend:https:0"]
                      ?? builder.Configuration["services:webfrontend:http:0"];
    if (!string.IsNullOrEmpty(frontendUrl))
    {
        app.MapGet("/", () => Results.Redirect(frontendUrl));
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
