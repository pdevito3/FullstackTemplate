# Source generators

A source generator writes code at build time. Reflection reads types at run time. Use a generator when one does the job. Reflection is slow on the first call for each type. It breaks under trimming and native AOT. A rename does not fail the build. With a generator, the work moves to the build, and a mistake becomes a compile error.

## The generators of the template

| Generator | Package or project | It makes | Example |
|-----------|--------------------|----------|---------|
| Mapperly | `Riok.Mapperly` | The map of each DTO and the `IQueryable` projection of each list query | `FullstackTemplate.Server/Domain/Agents/Mappings/AgentMapper.cs` |
| Regex generator | `[GeneratedRegex]` of .NET | A compiled matcher for a static partial method | `FullstackTemplate.Server/Resources/Entrypoints/Http/BindingProblem.cs` |
| JSON generator | `[JsonSerializable]` of `System.Text.Json` | The JSON metadata and the serializer of each listed type | `FullstackTemplate.Server/Resources/Entrypoints/EntrypointJsonContext.cs` |
| `ValueObjectGenerator` | `FullstackTemplate.SourceGenerators` | `Equals` and `GetHashCode` of each `partial` value object | `FullstackTemplate.Server/Domain/EmailAddresses/EmailAddress.cs` |
| `EntityIdPrefixAnalyzer` | `FullstackTemplate.SourceGenerators` | No code. It checks the `IdPrefix` constant and the constructors of each entity at compile time | `FullstackTemplate.SourceGenerators/EntityIdPrefixAnalyzer.cs` |
| `GraphGraftGenerator` | `tests/FullstackTemplate.EntityGraphs.Generators` | The `AddX` and `AddXs` methods of the test entity graph | `tests/FullstackTemplate.SharedTestHelpers/EntityGraphs/Blueprints.cs` |
| gRPC generator | `Grpc.Tools` | The service base classes from each `.proto` file. The functional tests make the clients | The `<Protobuf>` item in `FullstackTemplate.Server/FullstackTemplate.Server.csproj` |
| OpenAPI XML comments | `Microsoft.AspNetCore.OpenApi` | The descriptions of the OpenAPI document from the XML doc comments | `GenerateDocumentationFile` in the Server project. The unit tests add `Microsoft.AspNetCore.OpenApi.Generated` to `InterceptorsNamespaces` |
| OpenAPI document | `Microsoft.Extensions.ApiDescription.Server` | `FullstackTemplate.Server/openapi/FullstackTemplate.Server.json` in a Debug build. `pnpm generate:api` makes the SPA client from it | `OpenApiGenerateDocuments` in the Server project |

## Use a generator

Use a generator for code that runs on each request. Also use one for code that must work under trimming or native AOT. Use one for these jobs:

- **A map.** Write a Mapperly mapper (see [dtos-and-mappings.md](dtos-and-mappings.md)). Do not write a reflection mapper.
- **A regex.** Put `[GeneratedRegex]` on a static partial method. The build refuses each `Regex` constructor and each static `Regex` method that takes a pattern.
- **JSON that the Server writes itself.** Add the type to `EntrypointJsonContext` with `[JsonSerializable]`. Then call the `JsonSerializer` overload that takes a `JsonTypeInfo`, for example `EntrypointJsonContext.Default.PaginationMetadata`. If the code is not in an entrypoint, add a context next to the code.
- **A value object.** Make the class `partial` and derive it from `ValueObject`. `ValueObjectGenerator` writes its equality.
- **Test data.** Add a blueprint to the entity graph. Use the `test-entity-graph` skill. Do not set private entity state with reflection.

## Reflection is allowed

Reflection is allowed in these kinds of code only:

| Kind | Why it is fine | The sites of today |
|------|----------------|--------------------|
| A test or an architecture check | It does not run in the app | `tests/FullstackTemplate.UnitTests/Architecture/BoundaryScanner.cs` reads the IL with Mono.Cecil |
| A startup scan | It runs one time | Scrutor in `Resources/Extensions/ServiceRegistrationExtension.cs` and `Databases/DomainEventRegistration.cs`. `RegisterServicesFromAssembly` of MediatR in `Resources/Extensions/WebAppServiceConfiguration.cs`. `WithToolsFromAssembly` of MCP in `Resources/Entrypoints/EntrypointRegistration.cs`. `ApplyConfigurationsFromAssembly` in `Databases/AppDbContext.cs`. `Resources/Entrypoints/Http/EndpointDiscovery.cs`. `Databases/Auditing/AuditModel.cs`, `AuditVocabulary.cs`, and `AuditSensitivity.cs`, which run one time for the EF model |
| An OpenAPI transformer | It runs when the document is made, not for each API request | `Resources/Entrypoints/Http/OpenApi/FixedNamesTransformer.cs` reads `[NamesOf]` |
| A per-type cache | It reads each type one time, then keeps the result | `RequestRules` in `Resources/RequestValidationBehavior.cs`. The runner of each event type in `Databases/DomainEventDispatcher.cs`. `IdPrefix<TEntity>` in `Databases/QueryableExtensions.cs` |

The paths in the table are in `FullstackTemplate.Server/`, except the test.

Two sites of today fit no kind. They read a type each time that they run, with no cache:

- `AuditAction.NameOf` in `Domain/AuditEntries/AuditAction.cs` reads `[AuditActionName]` on each save that records an action.
- `WrongTypeArguments` in `Resources/Entrypoints/Mcp/McpDomainErrors.cs` reads the parameters of a tool method. It runs only after a tool call fails.

If a site calls a banned API, suppress RS0030 on the method. Start the justification with the kind, for example `Justification = "Per-type cache. ..."`. Then add the file to the list of `BannedApiSuppressionTests`. If new reflection calls an API that is not banned, put a comment above it. The comment names the kind.

## Before you write reflection

1. Name the job: a map, a match, a JSON payload, a value object, test data, or a type scan.
2. Find the job in the table of generators. If a generator does the job, use it.
3. If the code is one of the kinds that allow reflection, write the reflection. On a request path, cache the result for each type.
4. If neither step fits, stop. Name the case in the pull request.

## The build rule

`FullstackTemplate.Server/BannedSymbols.txt` bans these APIs in the Server (RS0030). The error names the fix.

- Each `Regex` constructor, and each static `Regex` method that takes a pattern: `IsMatch`, `Match`, `Matches`, `Split`, `Replace`, `Count`, `EnumerateMatches`, and `EnumerateSplits`.
- `Activator.CreateInstance(Type)` and `Activator.CreateInstance(Type, object[])`.
- `Type.GetProperties()` and `Type.GetProperties(BindingFlags)`.

The build does not ban `GetCustomAttribute`, `MakeGenericType`, `MakeGenericMethod`, `GetField`, or the `JsonSerializer` overloads with no `JsonTypeInfo`. The `source-generators` skill and the review catch these.

## JSON

`EntrypointJsonContext` has the types that the entrypoints serialize themselves: the `X-Pagination` header (`PaginationMetadata`) and the structured content of an MCP tool error (`McpToolError`). `AddEntrypoints` puts the context first in the `TypeInfoResolverChain` of the HTTP JSON options.

These calls still use the reflection fallback:

- The bodies of the minimal API endpoints. The endpoint DTOs are not in a context.
- `AuditValues.WriteAction`. The app and the tests declare the action types, so the Server cannot list them.
- `McpDomainErrors.Converts`. It reads the parameter type of a tool at run time, and it runs only after a tool call fails.
- `FullstackTemplate.Bff/User/UserEndpoint.cs`.

Keep the fallback until the template aims at native AOT. At that time, add the DTOs to a context, remove the fallback, and ban the `JsonSerializer.Serialize` and `JsonSerializer.Deserialize` overloads that take no `JsonTypeInfo`.

## Logging

Serilog has no source generator, and the template logs with Serilog. Use a message template with named properties through `Here()`, as [feature-telemetry.md](feature-telemetry.md) says. If the template moves to `Microsoft.Extensions.Logging`, use `[LoggerMessage]`.

## Next steps for native AOT

The template does not aim at native AOT now. If it does, these are the next steps:

- Publish the Server with native AOT.
- Use a source-generated mediator in place of MediatR.
- Use an EF Core compiled model (`dotnet ef dbcontext optimize`).
