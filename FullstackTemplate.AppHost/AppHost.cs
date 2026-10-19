var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.FullstackTemplate_Server>("server")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

// The proxy keeps 5173 in a normal run. An isolated run replaces it with a random port.
builder.AddViteApp("webfrontend", "../frontend")
    .WithEndpoint("http", endpoint => endpoint.Port = 5173)
    .WithPnpm();

builder.Build().Run();
