using System.Globalization;
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

    var builder = WebApplication.CreateBuilder(args);

    builder.AddServiceDefaults(); // OpenTelemetry, Serilog, health checks, resilience, service discovery
    builder.ConfigureServices();

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

    app.UseExceptionHandler();

    app.MapDefaultEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    // A startup failure must not look like a clean exit to Aspire or CI.
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
