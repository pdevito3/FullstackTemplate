using System.Globalization;
using Microsoft.EntityFrameworkCore;
using FullstackTemplate.Server;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.Server.Resources.Entrypoints;
using FullstackTemplate.Server.Resources.Entrypoints.Http.OpenApi;
using FullstackTemplate.Server.Resources.Extensions;
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
    Log.Information("Starting FullstackTemplate.Server");

    var builder = WebApplication.CreateBuilder(OpenApiBuildTime.WithHostArgs(args));
    CSharpReplGuard.ThrowIfLoadedOutsideDevelopment(builder.Environment);

    builder.AddServiceDefaults(); // OpenTelemetry, Serilog, health checks, resilience, service discovery
    builder.ConfigureServices();
    builder.AddEntrypoints();
    builder.Services.AddHostedService<MigrationHostedService<AppDbContext>>();
    OpenApiBuildTime.RemoveHostedServices(builder.Services);

    var app = builder.Build();

    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = HostExtensions.RequestLogLevel;
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    app.UseEntrypoints();
    app.MapEntrypoints();

    app.MapDefaultEndpoints();

    app.Run();
}
// The build-time OpenAPI generator stops the host on purpose with a HostAbortedException.
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    // A startup failure, such as a CSharpReplGuard stop, must not look like a clean exit to Aspire or CI.
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

