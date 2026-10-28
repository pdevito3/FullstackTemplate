# Endpoints

An endpoint is an adapter. It gets a request from a transport, sends a MediatR request, and returns the response of that transport. It has no other logic. The Server has three transports: the HTTP API, gRPC, and MCP. All three use the same JWT bearer authentication, so each one sees the same caller and the same tenant scope.

Each adapter lives in the folder of its feature, next to the request and the handler:

```
Domain/Tenants/
├── Features/
│   └── GetTenant/
│       ├── GetTenant.cs           # Core: the MediatR query and its handler
│       ├── GetTenant.Http.cs      # HTTP: GetTenantEndpoint, an IEndpoint
│       ├── GetTenant.Grpc.cs      # gRPC: the GetTenant method of the partial TenantsGrpcService
│       └── GetTenant.Mcp.cs       # MCP: GetTenantTool, an [McpServerToolType] static class
└── Grpc/
    └── v1/
        ├── tenants.proto          # The gRPC v1 contract
        ├── TenantsGrpcService.cs  # The root of the partial service class
        └── TenantGrpcMappings.cs  # The maps from the DTOs to the messages
```

Each adapter is optional. A feature has only the adapters that it needs, and it can have none. For example, a feature that only a domain event handler or a background job sends has no adapter. A feature with no file for a transport is not exposed on that transport. Now, all of the Agents, AuditEntries, Tenants, Users, and Weather features have HTTP. Only `GetTenant`, `GetTenantList`, and `AddTenant` have gRPC. Only `GetTenant`, `GetTenantList`, `AddTenant`, and `UpdateTenant` have MCP.

The reason for this layout is in [ADR 0002](../adr/0002-colocate-entrypoints-with-features.md).

## Choose the entrypoints of a feature

Do these steps for each new feature:

1. Find the callers of the feature. The SPA uses HTTP through the BFF. A service client can use HTTP or gRPC. An AI agent uses MCP.
2. If you do not know the callers, ask. Do not add an adapter because other features have one.
3. Add one file for each transport that a caller needs: [HTTP](#http), [gRPC](#grpc), or [MCP](#mcp).
4. If no caller is outside the Server, add no adapter.

To expose an existing feature on one more transport, add only the file of that transport. The core file and the other adapters do not change.

The shared plumbing is in `Resources/Entrypoints/`: the registration, the problem details, auth, the OpenAPI transformers, the gRPC error interceptor, and the MCP error filter.

## Rules for each adapter

1. Send a MediatR request. Do not inject `AppDbContext` and do not use EF Core. `EntrypointBoundaryTests` fails if an adapter uses the database.
2. Do not put transport types in the core. A request, a handler, an entity, a DTO, or a service must not use `Microsoft.AspNetCore.Http`, `Microsoft.AspNetCore.Mvc`, `Microsoft.AspNetCore.Routing`, gRPC, Protobuf, or MCP. `EntrypointBoundaryTests` fails if it does. When the core needs a value of the request, add an interface in the core and implement it in `Resources/Entrypoints/`. For example, `CurrentUserService` reads the caller from `ICurrentPrincipalAccessor`, and `HttpContextPrincipalAccessor` gives the principal of the HTTP request.

`BoundaryScanner` in the unit tests applies these rules. It reads the compiled code, so it also sees a method body, an attribute argument, and a generic constraint. The tests also run it on `tests/FullstackTemplate.ArchitectureFixtures`, which has one violation for each rule. If the scan stops finding a violation, that test fails. `HostExtensions` wires the host, the same as `Program`, so the scan does not check it. Add a type to this list or to the allowed transport types only with a reason in `EntrypointBoundaryTests`.
3. Do not catch exceptions. The shared plumbing of each transport maps the domain exceptions: problem details for HTTP, a status code for gRPC, and a tool error for MCP. See [backend.md](../backend.md#error-responses).

## HTTP

An HTTP endpoint is a minimal API. The example is `Domain/Users/Features/AddUser/AddUser.Http.cs`.

1. Add a `public sealed class [FeatureName]Endpoint : IEndpoint` in `[FeatureName].Http.cs`. Use the namespace of the feature, for example `FullstackTemplate.Server.Domain.Users.Features`.
2. `EndpointDiscovery` finds each `IEndpoint` with a reflection scan at startup. It calls `Map` with the group `api/v{version:apiVersion}`, so the route in `Map` has no version, for example `/users/{id}`. Do not register the endpoint by hand.
3. Declare the version with `.MapToApiVersion(ApiVersions.V1)`. `ApiVersions` has one definition of each version. Do not write `new ApiVersion(1, 0)` in an endpoint.
4. Give each endpoint `.RequireAuthorization()`, or `.AllowAnonymous()` when the endpoint is public.
5. Give each endpoint a name with `.WithName`. `CreatedAtRoute` uses the name, and the frontend client uses it as the operation name. If an endpoint has no name, the Debug build fails.
6. Give each endpoint the tag of its entity with `.WithTags`, for example `"Users"`.
7. Make the handler a `public static` method named `Handle`, and give it an XML `<summary>`. The OpenAPI document reads the summary only from a public method.
8. Return `TypedResults` with a typed return, for example `Task<Ok<UserDto>>`. Use a `Results<...>` union when the handler returns more than one result type. The OpenAPI document reads the success response from the return type.
9. Declare each error status that the endpoint can return. Use `.ProducesValidationProblem()` for 400, `.ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)` for 422, `.ProducesValidationProblem(StatusCodes.Status409Conflict)` for 409, and `.ProducesProblem(...)` for 403 and 404. See [backend.md](../backend.md#error-responses) for the statuses that each endpoint declares.
10. Do not add a route constraint to an entity ID. When the ID is malformed, no entity has it, so the API returns 404.
11. Do not add the `X-Pagination` header in the endpoint. `PaginationHeaderFilter` is on the versioned group, and it adds the header to each `PagedList<T>` result.
12. End the name of each list endpoint in `List`, and use `List` for no other endpoint. The frontend client of a list endpoint returns the full response, so that the caller can read the header. If a name breaks this rule, the Debug build fails.
13. Give a list endpoint one handler parameter for each query value, with the camelCase name of the contract. Make each one nullable, and put the `[Range]` rules of the DTO on the page parameters. These copies give the limits to the OpenAPI contract only. `RequestValidationBehavior` applies the rules of the DTO, and a unit test fails if a copy is not the same as the rule of the DTO. Then make the parameters DTO with its `From` factory. All of the entrypoints use this factory, so that a missing value gets the same default on each transport. Do not bind the DTO with `[AsParameters]`. It gives PascalCase names, and it makes each property that is not nullable required.

### Example

```csharp
public sealed class GetUserEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/users/{id}", Handle)
        .WithName("GetUser")
        .WithTags("Users")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status404NotFound);

    /// <summary>
    /// Gets a single User by ID.
    /// </summary>
    public static async Task<Ok<UserDto>> Handle(string id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUser.Query(id), cancellationToken);
        return TypedResults.Ok(result);
    }
}
```

A create endpoint returns `TypedResults.CreatedAtRoute(result, "GetUser", new { id = result.Id })`. A delete endpoint returns `TypedResults.NoContent()`.

### HTTP methods

| Operation | Method | Route | Success status |
|-----------|--------|-------|----------------|
| Get one | GET | `/{entities}/{id}` | 200 |
| Get a list | GET | `/{entities}` | 200, with the `X-Pagination` header from `PaginationHeaderFilter` |
| Get a page of a log that only grows | GET | `/{entities}?cursor=` | 200, with `nextCursor` in the body. The name ends in `Page`, for example `GetAuditEntryPage`. See [audit-log.md](audit-log.md#read-the-audit-log). |
| Create | POST | `/{entities}` | 201, with `CreatedAtRoute` |
| Update | PUT | `/{entities}/{id}` | 200 |
| Delete | DELETE | `/{entities}/{id}` | 204 |
| Business action | POST or PUT | `/{entities}/{id}/action`, for example `/users/{id}/role` | 200 |

### Request validation

`RequestValidationBehavior` is a MediatR behavior, so it runs the same rules for HTTP, gRPC, and MCP. It runs the DataAnnotations rules of the request and of each DTO in the request, before the handler. It also runs the rules of each DTO in a DTO and of each element of a list of DTOs, at any depth. It does not check the elements of a list of lists or the values of a dictionary. It skips a `null` element of a list of DTOs, so the `null` element gets to the handler. Put only DTOs and values in a request, not a domain entity, because the behavior would walk the full graph of the entity, such as its value objects, its permission lists, and its domain events. A broken rule throws `RequestValidationException`. HTTP gives 400 with the errors of each camelCase field, gRPC gives `InvalidArgument`, and MCP gives an `invalid_argument` tool error. Give each string property of a request DTO that is not nullable `[RequiredField]`. Then a missing value gives 400, and an empty value goes to the domain rules, which give 422. Do not use `[Required]` for these properties. If an adapter sends a value of the DTO, not the DTO, put the rule on the property of the command, for example `[property: RequiredField] string Role`. The key of a field of the request or of its body DTO is the JSON name of the field, for example `lastName`. The key of a field of a DTO below them has the path of the field, for example `shipTo.city` or `lines[1].name`. The message names only the field, because a form shows it under the field. For example, the error under `lines[1].name` is "The name field is required."

A value that does not bind, such as `pageNumber=abc` or a body that is not valid JSON, also gives 400 with errors. `RouteHandlerOptions.ThrowOnBadRequest` makes the endpoint throw, and `DomainProblemDetails` shows the error. `BindingProblem` gives the message, for example "The value 'abc' is not valid for pageNumber." A body value of the wrong type, such as a text, a null, or a value out of range for a number, gives a message that names the kind of value that the field takes. For example, `version` gives "The version field is not a valid whole number, or it is out of range." The JSON error is the same for a text, `1.5`, and a value out of range, so the message for a whole number or a decimal names both causes. The kinds are a whole number, a number, a date and time, and a date. A field of another type gives "The name field is not valid." A bad item of a list, such as `"x"` in `"counts": [1, "x"]`, gives "An item of the counts field is not a valid whole number, or it is out of range." under the key `counts[1]`. The error is under the key of the field, for example `version` or `lines[1].hours`. A body value that is not valid JSON, such as `1.2.3` for a number, gives "The version field is not valid JSON." under the key of the field. Other JSON that does not parse, such as a body that is not JSON or a missing comma between members, gives "The request body is not valid JSON." under the empty key. A body that stops before its end, such as `{ "name": `, gives the error under the last member that the reader got to, for example "The name field is not valid JSON." A JSON error that a custom converter throws keeps its `$.path` key and its own message. As for request validation, the message names the field, not its path, so the error under `lines[1].hours` is "The hours field is not a valid number, or it is out of range." The framework stops at the first value that does not bind, so the 400 names only that value. This is an accepted limit.

### HTTP versions

Add a new version when a change breaks callers: a removed or renamed DTO property, a changed type, a changed route, or a removed endpoint. Add `V2` to `ApiVersions`. Then add a second endpoint class with `.MapToApiVersion(ApiVersions.V2)`, for example `GetUserV2Endpoint` in the same feature folder. Keep the v1 endpoint for the clients that use v1. Then add the version to `SupportedVersions`. See [backend.md](../backend.md#api-versioning).

## gRPC

The gRPC service of a domain is a partial class. The example is `Domain/Tenants/Grpc/v1/`.

1. Put the contract in `Domain/[EntityName]s/Grpc/v{n}/[entityname]s.proto`. Put the version in the package and in the C# namespace: `package fullstacktemplate.tenants.v1;` and `option csharp_namespace = "FullstackTemplate.Server.Domain.Tenants.Grpc.V1";`. The csproj compiles each `Domain/**/Grpc/v*/*.proto` with `GrpcServices="Server"`, so the Server has only the service base. A caller generates its own client from the proto. The functional tests do this with `GrpcServices="Client"`.
2. Give each RPC its own request and response message, for example `GetTenantRequest` and `GetTenantResponse`. `buf lint` fails if an RPC returns a shared message.
3. Put the root of the service in `Grpc/v{n}/[EntityName]sGrpcService.cs`: a `sealed partial` class with `[Authorize]` and a primary constructor that takes `IMediator`.
4. Put each RPC in the folder of its feature, in `[FeatureName].Grpc.cs`. Use the namespace of the service, not the namespace of the feature, so that the file can extend the partial class. Add `using global::Grpc.Core;`, because the name `Grpc` binds to the namespace segment.
5. Put a map that more than one RPC uses in `Grpc/v{n}/[EntityName]GrpcMappings.cs`.
6. If a field can have no value, mark it `optional`, for example the page fields of `ListTenantsRequest`. Read `Has[FieldName]`, and send `null` when it is false, so that the DTO gives its default. A field that is not `optional` has no unset state: `0` and `""` are values.
7. Map the service in `EntrypointRegistration.MapEntrypoints` with `MapGrpcService<T>()`.

gRPC needs HTTP/2. Without TLS, Kestrel serves HTTP/2 only on a port that serves only HTTP/2. So the Server has two named Kestrel endpoints in `Kestrel:Endpoints` of `appsettings.json`. The `grpc` endpoint serves gRPC in plaintext (h2c) on 8081, with `Protocols` set to `Http2`. The `http` endpoint serves the HTTP API, MCP, and the health checks on 8080 with HTTP/1.1. `appsettings.Development.json` adds `https` and the local ports. Aspire reads these names, makes one endpoint for each, and replaces each `Url`. Do not set `ASPNETCORE_URLS`, because Kestrel ignores it when the configuration has endpoints.

`DomainErrorInterceptor` maps the domain exceptions to status codes: `InvalidArgument` (a validation error or a bad input), `NotFound`, `PermissionDenied`, `AlreadyExists` (a conflict with a record that exists), and `Aborted` (a stale version, or a concurrent change after the last run again). If the error names request fields, the status has a `google.rpc.BadRequest` detail with one field violation for each message. These are the same fields as the `errors` of an HTTP problem. A client reads them with `RpcException.GetRpcStatus()` and `GetDetail<BadRequest>()`.

### gRPC versions

CI runs `buf lint` and `buf breaking` against main (see `buf.yaml`). A change that breaks the wire or the generated code fails the build: a removed field, a changed field number or type, a renamed RPC, or a changed `csharp_namespace`. Add a field with a new number instead.

To make a breaking change, add v2 next to v1:

1. Copy `Grpc/v1/` to `Grpc/v2/`. Set the package to `fullstacktemplate.tenants.v2` and the namespace to `...Tenants.Grpc.V2`.
2. Make the change in the v2 proto.
3. Add `[FeatureName].Grpc.V2.cs` in the folder of each feature that v2 serves, with the namespace `...Tenants.Grpc.V2`.
4. Map the v2 service next to v1 in `MapEntrypoints`. Both versions send the same MediatR requests.
5. Remove v1 when no caller uses it. `buf breaking` then reports the removed file, so remove it in a change that says so.

## MCP

Each MCP tool is a static class. The example is `Domain/Tenants/Features/GetTenantList/GetTenantList.Mcp.cs`.

1. Add a `public static class [FeatureName]Tool` with `[McpServerToolType]` in `[FeatureName].Mcp.cs`. Use the namespace of the feature. `WithToolsFromAssembly` finds the class.
2. Give it one `public static` method with `[McpServerTool(Name = "...", UseStructuredContent = true)]` and a `[Description]`. Set `ReadOnly = true` for a read, and `Destructive = false` for an add.
3. Take `IMediator` and `CancellationToken` as method parameters. The SDK resolves a registered service from the request services, so the service is not in the input schema.
4. Give each other parameter a `[Description]`. These parameters are the input schema.
5. Return the DTO. For a list, return `McpPage.From(result)`, so that the result has the page data.
6. Give the method exactly one `[ToolAccess]` attribute, next to `[McpServerTool]`. It names what the handler requires. See [Tool access](#tool-access).
7. Put no other access check in the tool. If the handler checks a permission, also name the permission in the `[Description]`, so that the model knows why a call can fail. The example is `Domain/Tenants/Features/UpdateTenant/UpdateTenant.Mcp.cs`.

`/mcp` needs a token. A `401` from `/mcp` gives the URL of the protected resource metadata (RFC 9728) of the server in the `resource_metadata` parameter of its Bearer challenge. The document is at `/.well-known/oauth-protected-resource/mcp`, and it names the identity provider and the scopes. `McpResourceMetadata` serves the document and adds the parameter to the challenge. See [How an MCP client finds the provider](../authentication.md#how-an-mcp-client-finds-the-provider).

### Tool access

`[ToolAccess]` has three forms:

| Attribute | Use it for | A caller sees and can call the tool if |
|---|---|---|
| `[ToolAccess(ToolAccessKind.Read)]` | A read with no permission check, for example `get_tenant` | The caller is recognized: each caller except an unknown client and an anonymous request. The tenant filter decides the rows. |
| `[ToolAccess(Permission.Names.ManageTenant)]` | A tool whose handler calls `RequireAsync` with that permission, for example `update_tenant` | `Caller.Has` gives true for the permission |
| `[ToolAccess(ToolAccessKind.ManageAllTenants)]` | A tool whose handler checks `Caller.ManagesAllTenants`, for example `add_tenant` | The caller can manage all tenants |

Use a `Permission.Names` constant, so that the compiler checks the name. If the handler checks more than one permission, name the permission that each call needs. The handler checks the others. If the handler lets a user act on their own record with `Caller.RequireSelfOr`, use `ToolAccessKind.Read`, so that the handler decides. A permission in the attribute would hide the tool from that user.

`ToolAccessKind.Read` is stricter on MCP than on HTTP and gRPC. An unknown client gets a `permission_denied` tool error that tells it to ask for a registration. Over HTTP and gRPC, the same caller gets an empty list or 404, because the tenant filter shows it no rows.

`McpToolAccess.ListFilter` removes from `tools/list` each tool that the caller cannot use. `McpToolAccess.CallFilter` denies a call to such a tool. It runs inside `McpDomainErrors.Filter`, so the denial is a `permission_denied` tool error with the message of `Caller`. Both filters load the caller with `IPermissionGuard.GetCallerAsync` and ask `Caller`, so the list, the call, and the handler give the same answer. For an agent, the caller holds only the permissions of its grant. See [Agents](../authentication.md#agents).

At startup, `McpToolAccess.Validate` stops the server if a tool has no `[ToolAccess]`, has a `ToolAccessKind` that is not defined, or names a permission that does not exist. A unit test runs the same check on each tool of the Server.

The attribute repeats the permission of the handler. If the two differ, the result fails closed: the list hides the tool from a caller who could use it, or the handler denies the call.

`McpDomainErrors` maps a domain exception to a tool error with `isError` set and the same fields as an HTTP problem. The `code` has the name of the gRPC status, for example `already_exists` for a conflict and `aborted` for a stale version or a concurrent change. A missing required argument gives an `invalid_argument` tool error with the message "The name field is required.", the same as a missing body field. An argument of the wrong JSON type gives an `invalid_argument` tool error with one message for each such argument, for example "The value 'ten' is not valid for pageSize.", the same as a query value that does not bind. A tool call that is not valid JSON does not get to the tool. The SDK returns 400 with the JSON-RPC error `-32600`.

`McpCallTelemetry.Filter` is the first call filter. It puts the tool name (`gen_ai.tool.name`), the caller tags, and the `error.type` of a failed call on the span of the HTTP request. A call that fails before its feature runs has no feature span, so this span is the only one that names the tool and the caller. The `error.type` is the `code` of a tool error, or the type of the exception that the SDK answers. The name of an unknown tool comes from the caller, so the tag has its first 128 characters, with the control characters removed. A call to an unknown tool gets the JSON-RPC error `-32602` of the SDK. The SDK logs this error at `Error`, but it is a client error, so `McpProtocolErrorLogFilter` drops the log.

### MCP versions

MCP has no version segment. The contract of a tool is its name and its input schema.

- You can add an optional parameter, or change a description. A caller that does not know the change still works.
- A breaking change gets a new tool name, for example `list_tenants_v2`: a removed or renamed parameter, a new required parameter, a changed type, or a changed meaning. Add the new tool as a new static class.
- Keep the old tool, and mark it deprecated in its description, for example: "Deprecated: use list_tenants_v2. Gets one page of ...". Remove it when no caller uses it.
