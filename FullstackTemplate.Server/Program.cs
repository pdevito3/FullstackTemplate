using System.Security.Claims;
using Asp.Versioning;
using Microsoft.EntityFrameworkCore;
using FullstackTemplate.Server;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.Server.Resources.Extensions;
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
    Log.Information("Starting FullstackTemplate.Server");

    var builder = WebApplication.CreateBuilder(args);
    CSharpReplGuard.ThrowIfLoadedOutsideDevelopment(builder.Environment);

    builder.AddServiceDefaults(); // OpenTelemetry, Serilog, health checks, resilience, service discovery
    builder.ConfigureServices();
    builder.Services.AddHostedService<MigrationHostedService<AppDbContext>>();

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

    app.UseExceptionHandler(new ExceptionHandlerOptions
    {
        StatusCodeSelector = DomainProblemDetails.StatusCodeFor,
        // A client error is an expected outcome, so it is not logged as an unhandled exception.
        SuppressDiagnosticsCallback = context => DomainProblemDetails.IsClientError(context.Exception),
    });

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwaggerExtension(builder.Configuration);
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // API Versioning
    var apiVersionSet = app.GetApiVersionSet();

    string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];
    const int minTemperatureC = -20;
    const int maxTemperatureC = 55;

    // The summary matches the temperature: the range splits into one equal band for each summary.
    string SummaryFor(int temperatureC)
        => summaries[(temperatureC - minTemperatureC) * summaries.Length / (maxTemperatureC - minTemperatureC)];

    var api = app.MapGroup("api/v{version:apiVersion}")
        .WithApiVersionSet(apiVersionSet);

    api.MapGet("/weather", () =>
    {
        using var activity = Telemetry.Trace([], "GenerateWeatherForecast");

        var forecast = Enumerable.Range(1, 5).Select(index =>
            {
                var temperatureC = Random.Shared.Next(minTemperatureC, maxTemperatureC);
                return new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    temperatureC,
                    SummaryFor(temperatureC)
                );
            })
            .ToArray();

        activity?.SetTag("forecast.count", forecast.Length);

        return forecast;
    })
    .WithName("GetWeatherForecast")
    .MapToApiVersion(new ApiVersion(1, 0));

    api.MapGet("/weather/secure", (ClaimsPrincipal user) =>
    {
        using var activity = Telemetry.Trace([], "GenerateSecureWeatherForecast");

        var userName = user.Identity?.Name ?? user.FindFirstValue("sub") ?? "Unknown";

        var forecast = Enumerable.Range(1, 5).Select(index =>
            {
                var temperatureC = Random.Shared.Next(minTemperatureC, maxTemperatureC);
                return new SecureWeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    temperatureC,
                    SummaryFor(temperatureC),
                    userName
                );
            })
            .ToArray();

        activity?.SetTag("forecast.count", forecast.Length);

        return forecast;
    })
    .RequireAuthorization()
    .WithName("GetSecureWeatherForecast")
    .MapToApiVersion(new ApiVersion(1, 0));

    app.MapDefaultEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    // A startup failure, such as a CSharpReplGuard stop, must not look like a clean exit to Aspire or CI.
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record SecureWeatherForecast(DateOnly Date, int TemperatureC, string? Summary, string RequestedBy)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
