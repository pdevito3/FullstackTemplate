# Colocate the entrypoints with their features

The Server has three transports: the HTTP API, gRPC, and MCP. Each adapter of a transport is a file in the folder of its feature, next to the MediatR request and its handler, for example `Features/AddTenant/AddTenant.Http.cs`. We do not put the adapters in a separate host project, and we do not group them by transport. As a result, a developer or a coding agent finds the request, the handler, and every way that the feature is exposed in one folder.

Each adapter is optional. A feature has only the adapters that its callers need, and it can have none.

## Considered options

- **A separate host project.** `FullstackTemplate.Server` becomes a class library for the core, and a `FullstackTemplate.Host` project holds all of the entrypoints. The compiler then stops the core from using transport types. We did not choose this option. It puts the adapters of one feature in three folders, far from the handler, and it changes many paths in CI, Docker, Kubb, the docs, and the skills. A project boundary also cannot block `Microsoft.AspNetCore.Mvc`, because that is part of the shared framework, so it still needs a boundary test.
- **Adapters grouped by transport in each domain**, for example `Domain/Tenants/Entrypoints/Http/`. We did not choose this option, because one controller, one gRPC service, and one tool class for each domain are far from the handlers that they call.

## Consequences

- `EntrypointBoundaryTests` reads the compiled code and enforces the boundary. A core type must not use a transport type, and an adapter must not use `AppDbContext` or EF Core. This test fails only when the tests run, not when you compile.
- HTTP uses minimal APIs, so that each HTTP endpoint is one small class. `EndpointDiscovery` finds each `IEndpoint`, and `WithToolsFromAssembly` finds each MCP tool. Thus a new adapter needs no change to shared code. The exception is a new gRPC service, which needs one `MapGrpcService` line.
- The gRPC service of a domain is a `partial` class, because the generated base class needs one class for each service. Each `.Grpc.cs` file uses the namespace of the service, not the namespace of its feature folder.
- Consider a separate host project again if a transport must deploy or scale on its own, or if a non-web host must use the core without the transport packages.
