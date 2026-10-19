using System.Globalization;
using FullstackTemplate.AppHost;
using Serilog;
using Serilog.Events;

// Bootstrap logger to catch startup errors before host is built
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Aspire.Hosting.Dcp", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting FullstackTemplate.AppHost");

    var builder = DistributedApplication.CreateBuilder(args);

    builder.Services.AddSerilog((_, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .Enrich.WithProperty("Application", "FullstackTemplate.AppHost"));

    var server = builder.AddProject<Projects.FullstackTemplate_Server>("server")
        .WithHttpHealthCheck("/health")
        .WithExternalHttpEndpoints();

    // The proxy keeps 5173 in a normal run. An isolated run replaces it with a random port.
    var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
        .WithEndpoint("http", endpoint => endpoint.Port = 5173)
        .WithPnpm();

    var authProvider = AuthProviders.DuendeDemo();
    server.WithServerAuth(authProvider);

    var bff = builder.AddProject<Projects.FullstackTemplate_Bff>("bff")
        .WithBffAuth(authProvider)
        .WithReference(server)
        .WithReference(webfrontend)
        .WithHttpHealthCheck("/health")
        .WithExternalHttpEndpoints()
        .WithParentRelationship(webfrontend);

    webfrontend.WithReference(bff);

    builder.Build().Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AppHost terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
