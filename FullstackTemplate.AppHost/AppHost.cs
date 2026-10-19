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

    // `aspire run --isolated` gives each proxied endpoint a random port and skips the data volumes,
    // so that worktrees can run the full stack at the same time. See Isolation.cs.
    var postgres = builder.AddPostgres("postgres")
        .WithImageTag(Config.PostgresImageTag)
        .WithDataVolumeUnlessIsolated();
    var appDb = postgres.AddDatabase("appdb");

    var server = builder.AddProject<Projects.FullstackTemplate_Server>("server")
        .WithReference(appDb)
        .WaitFor(appDb)
        .WithHttpHealthCheck("/health")
        .WithExternalHttpEndpoints()
        .WithCSharpRepl();
    postgres.WithParentRelationship(server);

    // The proxy keeps 5173 in a normal run. An isolated run replaces it with a random port.
    var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
        .WithEndpoint("http", endpoint => endpoint.Port = 5173)
        .WithPnpm();

    var authClientUrls = new AuthClientUrls(webfrontend.GetEndpoint("http"), server.GetEndpoint("https"));
#if LOCAL_DEV
    // var authProvider = AuthProviders.FusionAuth(builder, authClientUrls);
    // var authProvider = AuthProviders.Keycloak(builder, authClientUrls);
    var authProvider = AuthProviders.DuendeDemo();
    // var authProvider = AuthProviders.Authentik(builder, authClientUrls);
#else
    //#if (UseFusionAuth)
    var authProvider = AuthProviders.FusionAuth(builder, authClientUrls);
    //#elseif (UseKeycloak)
    var authProvider = AuthProviders.Keycloak(builder, authClientUrls);
    //#elseif (UseAuthentik)
    var authProvider = AuthProviders.Authentik(builder, authClientUrls);
    //#else
    var authProvider = AuthProviders.DuendeDemo();
    //#endif
#endif
    server.WithServerAuth(authProvider);

    var bff = builder.AddProject<Projects.FullstackTemplate_Bff>("bff")
        .WithBffAuth(authProvider)
        .WithReference(server)
        .WithReference(webfrontend)
        .WithHttpHealthCheck("/health")
        .WithExternalHttpEndpoints()
        .WithParentRelationship(webfrontend);

    webfrontend.WithReference(bff);

    // The BFF serves the SPA in production. The server is an internal API only.
    bff.PublishWithContainerFiles(webfrontend, "wwwroot");

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
