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
        // The endpoints come from Kestrel:Endpoints in the appsettings of the Server, with the HTTP/2-only "grpc"
        // endpoint. Aspire makes one endpoint for each name and sets its Url.
        .WithCSharpRepl();
    postgres.WithParentRelationship(server);

    // The proxy keeps 5173 in a normal run. An isolated run replaces it with a random port.
    var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
        .WithEndpoint("http", endpoint => endpoint.Port = 5173)
        .WithPnpm();

    var bff = builder.AddProject<Projects.FullstackTemplate_Bff>("bff")
        .WithReference(server)
        .WithReference(webfrontend)
        .WithHttpHealthCheck("/health")
        .WithExternalHttpEndpoints()
        .WithParentRelationship(webfrontend);

    var authClientUrls = new AuthClientUrls(
        webfrontend.GetEndpoint("http"), server.GetEndpoint("https"), bff.GetEndpoint("https"));
#if LOCAL_DEV
    // The AuthProvider setting selects the provider, for example the environment variable AuthProvider=Keycloak.
    // The default is the public Duende demo. The e2e tests use Keycloak, which runs in the stack.
    var authProvider = builder.Configuration["AuthProvider"] switch
    {
        null or "" or "DuendeDemo" => AuthProviders.DuendeDemo(),
        "Keycloak" => AuthProviders.Keycloak(builder, authClientUrls),
        "FusionAuth" => AuthProviders.FusionAuth(builder, authClientUrls),
        "Authentik" => AuthProviders.Authentik(builder, authClientUrls),
        var other => throw new InvalidOperationException(
            $"AuthProvider '{other}' is not known. Use DuendeDemo, Keycloak, FusionAuth, or Authentik."),
    };
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
    bff.WithBffAuth(authProvider);

    webfrontend.WithReference(bff);

    // The BFF serves the SPA in production. The server is an internal API only.
    bff.PublishWithContainerFiles(webfrontend, "wwwroot");

    // Labcoat finds the test projects of the solution and shows a web UI to run them. Aspire tests and publish skip it.
    builder.AddLabcoat("tests");

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
