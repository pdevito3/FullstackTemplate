using FullstackTemplate.AppHost;
using Serilog;
using Serilog.Events;

// Bootstrap logger to catch startup errors before host is built
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Aspire.Hosting.Dcp", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
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

#if LOCAL_DEV
    // var authProvider = AuthProviders.FusionAuth(builder);
    // var authProvider = AuthProviders.Keycloak(builder);
    var authProvider = AuthProviders.DuendeDemo();
    // var authProvider = AuthProviders.Authentik(builder);
#else
    //#if (UseFusionAuth)
    var authProvider = AuthProviders.FusionAuth(builder);
    //#elseif (UseKeycloak)
    var authProvider = AuthProviders.Keycloak(builder);
    //#elseif (UseAuthentik)
    var authProvider = AuthProviders.Authentik(builder);
    //#else
    var authProvider = AuthProviders.DuendeDemo();
    //#endif
#endif

    var postgres = builder.AddPostgres("postgres")
        .WithDataVolume($"{Config.VolumePrefix}-db-volume");
    var appDb = postgres.AddDatabase("appdb");

    var server = builder.AddProject<Projects.FullstackTemplate_Server>("server")
        .WithReference(appDb)
        .WaitFor(appDb)
        .WithServerAuth(authProvider)
        .WithHttpHealthCheck("/health")
        .WithExternalHttpEndpoints();
    postgres.WithParentRelationship(server);

    var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
        .WithEndpoint("http", endpoint =>
        {
            endpoint.Port = 5173;
            endpoint.IsProxied = false;
        })
        .WithPnpm();

    var bff = builder.AddProject<Projects.FullstackTemplate_Bff>("bff")
        .WithBffAuth(authProvider)
        .WithReference(server)
        .WithReference(webfrontend)
        .WithHttpHealthCheck("/health")
        .WithExternalHttpEndpoints()
        .WithParentRelationship(webfrontend);

    if (authProvider.AuthResource is not null)
    {
        server.WaitFor(authProvider.AuthResource);
        bff.WaitFor(authProvider.AuthResource);
    }

    webfrontend.WithReference(bff);

    server.PublishWithContainerFiles(webfrontend, "wwwroot");

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
