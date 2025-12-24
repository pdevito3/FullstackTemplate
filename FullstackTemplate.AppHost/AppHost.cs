using FullstackTemplate.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// ============================================================================
// Authentication Provider Selection
// ============================================================================
// Switch providers by commenting/uncommenting one line:

// var authProvider = AuthProviders.FusionAuth(builder);
var authProvider = AuthProviders.Keycloak(builder);
// var authProvider = AuthProviders.DuendeDemo();

// ============================================================================
// Application Services
// ============================================================================

var server = builder.AddProject<Projects.FullstackTemplate_Server>("server")
    .WithServerAuth(authProvider)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

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
    .WaitFor(server)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

if (authProvider.AuthResource is not null)
{
    server.WaitFor(authProvider.AuthResource);
    bff.WaitFor(authProvider.AuthResource);
}

webfrontend
    .WithReference(bff)
    .WaitFor(bff);

server.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();
