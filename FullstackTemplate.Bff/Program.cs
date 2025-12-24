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
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting FullstackTemplate.Bff");

    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .Enrich.WithProperty("Application", "FullstackTemplate.Bff"));

    builder.AddServiceDefaults();
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
        options.GetLevel = (httpContext, elapsed, ex) =>
        {
            if (ex != null)
                return LogEventLevel.Error;

            if (httpContext.Response.StatusCode >= 500)
                return LogEventLevel.Error;

            // Health checks are noisy, suppress them
            if (httpContext.Request.Path.StartsWithSegments("/health") ||
                httpContext.Request.Path.StartsWithSegments("/alive"))
                return LogEventLevel.Verbose;

            return LogEventLevel.Information;
        };

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

    app.MapBffManagementEndpoints();

    app.MapGet("/local-api/hello", () => new { message = "Hello from BFF!", timestamp = DateTime.UtcNow })
        .AsBffApiEndpoint();

    var serverUrl = builder.Configuration["services:server:https:0"]
        ?? builder.Configuration["services:server:http:0"]
        ?? "https://localhost:5001";

    app.MapRemoteBffApiEndpoint("/api", new Uri(serverUrl))
        .WithAccessToken(RequiredTokenType.UserOrNone);

    app.MapDefaultEndpoints();

    var frontendUrl = builder.Configuration["services:webfrontend:https:0"]
        ?? builder.Configuration["services:webfrontend:http:0"]
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL")
        ?? "http://localhost:5173";

    app.MapGet("/", () => Results.Redirect(frontendUrl));

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
