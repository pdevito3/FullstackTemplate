# Backend

`FullstackTemplate.Server` is the backend API. It uses .NET 10, CQRS with MediatR, and EF Core on PostgreSQL. The rules for each pattern are in `docs/backend/`.

## Domain structure

Each domain entity has this folder structure:

```
FullstackTemplate.Server/Domain/
└── [EntityName]s/
    ├── [EntityName].cs              # Domain entity
    ├── Dtos/                        # DTOs (read/write)
    ├── Features/
    │   └── [FeatureName]/           # One folder for each feature
    │       ├── [FeatureName].cs     # The CQRS command or query, and its handler
    │       ├── [FeatureName].Http.cs  # The HTTP endpoint, if the feature has one
    │       ├── [FeatureName].Grpc.cs  # The gRPC method, if the feature has one
    │       └── [FeatureName].Mcp.cs   # The MCP tool, if the feature has one
    ├── Grpc/v1/                     # The gRPC contract and service root, if the domain has gRPC
    ├── Mappings/                    # Mapperly mappers
    ├── Models/                      # Internal models
    └── DomainEvents/                # Domain events
```

Each entrypoint file is optional. A feature has only the entrypoints that its callers need. The shared plumbing of the entrypoints is in `Resources/Entrypoints/`. See [endpoints.md](backend/endpoints.md).

## Main concepts

- **Rich domain entities** have private setters, factory methods, and their own business logic.
- **CQRS with MediatR**: commands do the writes, and queries do the reads.
- **Vertical slices**: each feature is one folder. Its core file has the request and the handler. Its adapter files expose the feature over HTTP, gRPC, and MCP.
- **Mapperly** generates the DTO mapping code at compile time.
- **QueryKit** does the filters and sorts for list endpoints. They run on the DTO projection, so a caller can query only the fields that the contract shows. `CustomQueryKitConfiguration` limits the length and the nesting depth of a filter.
- **Value objects** (`EmailAddress`, `UserRole`, and more) use SmartEnum when they have fixed options.
- **Domain events** queue in the entity. `SaveChangesAsync` dispatches them.
- **Prefixed string IDs**: each entity has a `public const string IdPrefix`, for example `usr`. `EntityId.New` makes IDs like `usr_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f` (the prefix and a UUIDv7). The build gives an error when an entity has no prefix, an invalid prefix, or a prefix that another entity uses. It also gives an error when a constructor does not call `base(IdPrefix)` with the prefix of its own type, or when an entity derives from another entity. A UUIDv7 ID shows the creation time to the millisecond. Thus, if you hide the `CreatedOn` field, the creation time is not secret.

## Add a new feature

Do these steps in sequence. Each step has a doc in `docs/backend/`.

1. Create the domain entity ([domain-entities.md](backend/domain-entities.md)).
2. Add the entity configuration, then add a migration ([entity-configurations.md](backend/entity-configurations.md)).
3. Create the DTOs and the Mapperly mapper ([dtos-and-mappings.md](backend/dtos-and-mappings.md)).
4. Add the commands and queries ([features-and-cqrs.md](backend/features-and-cqrs.md)).
5. Add the endpoint, and the gRPC method and the MCP tool if the feature needs them ([endpoints.md](backend/endpoints.md)).
6. Add the domain events and their handlers ([domain-events.md](backend/domain-events.md)).
7. Tag each EF query with `TagWithOperationCallSite` (see [telemetry.md](telemetry.md)).
8. Build the Server, then run `pnpm generate:api` in `frontend/` (see [OpenAPI document](#openapi-document)).

## OpenAPI document

`Microsoft.AspNetCore.OpenApi` makes the OpenAPI document. Swagger UI shows it at `/swagger`. The Server maps `/swagger` and `/openapi/v1.json` only in Development. A Debug build also writes the document to `openapi/FullstackTemplate.Server.json`. Other configurations do not write it, because the Server csproj sets `OpenApiGenerateDocuments` to false for them. The frontend generates its API contract from this file (see [frontend.md](frontend.md#api-contract)).

The build starts the host without an auth provider or a database. `Resources/Entrypoints/Http/OpenApi/OpenApiBuildTime.cs` gives the host placeholder auth values for this run. This run does no OIDC discovery, so the committed file has no OAuth2 security scheme, but the runtime document at `/openapi/v1.json` has it. If startup code needs an external system, skip it when `OpenApiBuildTime.IsActive` is true.

The document transformers in `Resources/Entrypoints/Http/OpenApi/` set these rules:

- Each endpoint must have a name (`.WithName`). If an endpoint has no name, the Debug build fails.
- A property that is not nullable is required.
- A `PagedList<T>` response documents the `X-Pagination` header with the `PaginationMetadata` schema. `PaginationHeaderFilter` sets this header on each `PagedList<T>` result. It is an endpoint filter on the versioned group, so each endpoint that `EndpointDiscovery` maps has it.
- An endpoint that returns a `PagedList<T>` must have a name that ends in `List`. Other endpoints must not have a name that ends in `List`. If not, the Debug build fails. The frontend list clients use this name to return the full response.
- An endpoint that requires authentication documents a 401 with no body. An endpoint with `[AllowAnonymous]` or no authorization metadata gets no 401. Authorization has no roles or policies, so it cannot return 403. Declare a 403 with `.ProducesProblem(StatusCodes.Status403Forbidden)` on each endpoint that throws `ForbiddenAccessException`.
- Requests and success responses use only `application/json`. Error responses with a `ProblemDetails` body use only `application/problem+json`, as the Server sends them. The document types numbers only as numbers. The Server still accepts the lenient forms (`text/json` and `application/*+json` request bodies, and numbers in JSON strings), but the contract states only the canonical form.

## Service registration

`builder.Services.AddApplicationServices()` registers services automatically. The default lifetime is scoped. A marker interface changes the lifetime.

```csharp
// Scoped (no marker)
public interface IMyService { }
public class MyService : IMyService { }

// Singleton
public class MySingletonService : IMySingletonService, ISingletonService { }

// Transient
public class MyTransientService : IMyTransientService, ITransientService { }
```

## API versioning

The code is in `Resources/Entrypoints/Http/ApiVersioningExtension.cs`. The URL has the version segment `/api/v{version}/...`. `EndpointDiscovery` maps each `IEndpoint` on the group `api/v{version:apiVersion}`, and each endpoint declares its version:

```csharp
versionedGroup
    .MapGet("/customers/{id}", Handle)
    .WithName("GetCustomer")
    .MapToApiVersion(ApiVersions.V1);
```

Each endpoint serves only the versions that it declares. A request for a different version, such as `/api/v2/tenants`, gets 404. `SupportedVersions` in `ApiVersioningExtension.cs` is the list of all versions. Each version in this list gets its own OpenAPI document (`/openapi/v1.json`, `/openapi/v2.json`) and its own Swagger UI entry. A document contains only the operations of its version.

To add version 2:

1. Add `V2 = new(2, 0)` to `ApiVersions` in `Resources/Entrypoints/Http/ApiVersions.cs`, and add `ApiVersions.V2` to `SupportedVersions`.
2. Add a v2 endpoint class with `.MapToApiVersion(ApiVersions.V2)` in the feature folder. Keep the v1 endpoint for the clients that use v1. An endpoint that does not change can serve both versions with two `MapToApiVersion` calls.
3. Build the Server. The Debug build writes the v2 document to `openapi/FullstackTemplate.Server_v2.json`.
4. Run `pnpm generate:api`. `frontend/kubb.config.ts` reads each document in `openapi/`, and writes the v2 contract to `src/api/generated-v2`. `frontend/src/lib/api-client.ts` connects the client of each generated folder to `apiClient`.

To remove a version, remove it from `SupportedVersions`. Then delete its document in `openapi/` and its `src/api/generated-vN` folder. The build does not delete a document. If you keep the document, the `api-contract` job of the `CI` workflow fails.

## Error responses

All error responses use RFC 7807 problem details. `Exceptions/DomainError.cs` classifies the domain exceptions, and `Resources/Entrypoints/DomainProblemDetails.cs` maps them to HTTP. gRPC and MCP map the same classes (see [endpoints.md](backend/endpoints.md)):

| Exception | Status |
|-----------|--------|
| `ValidationException`, `FluentValidation.ValidationException` | 422. `detail` has the message. `errors` has the messages for each named property |
| `NotFoundException` | 404 |
| `ForbiddenAccessException` | 403. `detail` has the message |
| `ConflictException` from a duplicate check in a handler, and `ConstraintConflictException`, which `AppDbContext` throws when a unique index (Postgres `23505`) or an exclusion constraint (`23P01`) stops a save | 409 with the default `type`. `title` is "The request conflicts with a record that exists." `detail` has the message, and `errors` has it under the named field. A `ConstraintConflictException` does not show the constraint or the values |
| `StaleVersionException`, which `GuardVersion` throws for an old version in the request, and `AppDbContext` throws when another save changed the row after the load | 409 with `type` `urn:fullstacktemplate:problem:stale-version`. `title` is "The record changed after you loaded it." `detail` has the message, and `errors` has it under `version` |
| `RequestValidationException`, from a DataAnnotations rule in `RequestValidationBehavior` | 400. `errors` has the messages for each camelCase field |
| `QueryInputException`, for a `filters` or `sortOrder` value that QueryKit cannot apply | 400. `errors` has the message under the name of the parameter |
| Any other exception | 500 |

If a request value does not bind, or a DataAnnotations rule fails, the Server returns 400 with a `ValidationProblemDetails` body. This body has the `errors` field, keyed by the camelCase name of each field. Declare `.ProducesValidationProblem()` on each endpoint that can return 400, so that the OpenAPI document states this shape. This includes each list endpoint, because of the QueryKit row in the table.

A 422 comes from a domain rule or a FluentValidation rule. Its body also has the `ValidationProblemDetails` shape. Declare `.ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)` on each write endpoint (POST, PUT, PATCH, and DELETE). Also declare it when the endpoint has no request body, because a domain rule can fail in any write.

A 409 comes from a duplicate unique value, an overlap that an exclusion constraint stops, or a record that changed after the caller loaded it. Its body also has the `ValidationProblemDetails` shape. The `type` tells the cases apart. The type `urn:fullstacktemplate:problem:stale-version` (`DomainProblemDetails.StaleVersionType`) means that the caller must load the record again and repeat the change. Any other type means that the caller must change the value of the field in `errors`.

Each entrypoint shows the two kinds differently:

| Kind | HTTP | gRPC | MCP `code` |
|------|------|------|------------|
| Conflict with a record that exists | 409 with the default `type` | `AlreadyExists` | `already_exists` |
| Stale version | 409 with the stale version `type` | `Aborted` | `aborted` |

Declare `.ProducesValidationProblem(StatusCodes.Status409Conflict)` on each write endpoint that changes an existing record, because each one can lose a race with another save. Also declare it on each write endpoint that can add or change a unique value.

In a list handler, call `ApplyFilters` and `ApplySortOrder` from `Resources/QueryKitInputExtensions.cs`, not the QueryKit methods. They throw a `QueryInputException` with the name of the parameter. If an operator does not fit the type of a property, QueryKit fails with a .NET exception. Then the response gets a fixed message, and the trace gets the exception.

Do not catch these exceptions in an endpoint. Throw them from the entity or the handler.

Before you add a row with a unique value, check for the value in the handler and throw a `ConflictException` on the property. The check gives a clear message. Two requests can pass the check at the same time, so the unique index stops the second save, and the mapping above gives 409. Give each new unique index and each new exclusion constraint its request field in `ConstraintConflictException.ConstraintFields`. A test fails if one has no entry.

## Admin requests

A request that manages the users or the settings of a tenant implements `IRequireTenantAdmin` on its command. `TenantAdminBehavior` runs before the handler. It returns 403 if the current user is not an `Admin` in their tenant. A caller that can manage all tenants passes: a client in `Tenancy:ManageAllTenantsClientIds`, or system work in a system scope. See [multi-tenancy.md](multi-tenancy.md#row-scope-and-capability).

These commands need an admin: `AddUser`, `UpdateUser`, `DeleteUser`, `UpdateUserRole`, `AddUserPermission`, `RemoveUserPermission`, `UpdateTenant`, and `DeleteTenant`.

A command can also implement `IAllowSelf`. Then the user that the command is about also passes. `UpdateUser` uses it, so a user can update their own record. The update DTO has no role, so a user cannot make themselves an admin.

## Database

`Databases/AppDbContext.cs` has these features:

- **Soft delete**: the context filters out entities with `IsDeleted` automatically.
- **Audit fields**: the context sets `CreatedOn`, `CreatedBy`, `LastModifiedOn`, and `LastModifiedBy`.
- **Tenant filter**: see [multi-tenancy.md](multi-tenancy.md).
- **Domain events**: the context dispatches them after `SaveChangesAsync`.
- **Optimistic concurrency**: each update checks the version of the row. See [Concurrency](#concurrency).

Entity configurations are in `Databases/EntityConfigurations/`. Database names use snake case.

A paged list query must have a fixed sort order. Without one, a row can show on two pages or on no page. When the caller gives no sort, sort by a natural key and then by `Id`. After a custom sort, also sort by `Id`. Add an index on the default sort, so that the database can read the page in order. See `GetUserList.cs` and `UserConfiguration.cs` for an example.

### Concurrency

Each entity has `Version`, the Postgres `xmin` of its row. `MapVersionToXmin` maps it for each entity, so a new entity needs no configuration for it. EF Core adds `WHERE xmin = @loaded` to each update. When another save changed the row after the load, the save throws a `StaleVersionException`, which gives 409. The reasons for this design are in [ADR 0008](adr/0008-optimistic-concurrency-with-xmin.md).

- A save that adds, changes, or removes an owned child also updates the owner row. Thus the version of the root guards the whole aggregate. An owned child is the dependent of a `Cascade` foreign key (see [entity-configurations.md](backend/entity-configurations.md)). Change a child only through its loaded owner. The save throws when the owner of a changed child is not tracked.
- The read DTO of a root shows `version`. A PUT body that replaces the fields of an edit form has a required `version`, and the entity method calls `GuardVersion` first (see [domain-entities.md](backend/domain-entities.md#versions)). A command for one intent, such as a role change, a new permission, or a delete, has no version. The `xmin` check still stops its race between the load and the save.
- `Version` is an opaque number. Compare it only for equality. A list cannot filter or sort on `version`, and `ApplyFilters` and `ApplySortOrder` return 400 for it. Postgres has no order for `xid`, so a range or a sort fails. An equality filter works in Postgres, but a list has no use for it, so it also gives 400.

### Migrations

```bash
dotnet ef migrations add MyMigration --project FullstackTemplate.Server --startup-project FullstackTemplate.Server --output-dir Databases/Migrations
dotnet ef database update --project FullstackTemplate.Server --startup-project FullstackTemplate.Server
```

The server applies migrations at startup, with a Postgres advisory lock.

## Tests

| Project | Scope |
|---------|-------|
| `tests/FullstackTemplate.UnitTests` | Domain logic and small units, with no database |
| `tests/FullstackTemplate.IntegrationTests` | Feature handlers against a real PostgreSQL container |
| `tests/FullstackTemplate.FunctionalTests` | HTTP, gRPC, and MCP endpoints against one shared app and database |
| `tests/FullstackTemplate.SharedTestHelpers` | Fakes and builders for all test projects |

The tests use [TUnit](https://tunit.dev) on Microsoft.Testing.Platform, with Shouldly for assertions. Write `[Test]` for each test, `[Arguments(...)]` for an inline case, and `[MethodDataSource(nameof(...))]` for cases from a method or property.

The functional tests share one `TestingWebApplicationFactory`. Each test gets its own `FactoryClient`.

TUnit runs all tests in parallel, also the tests of one class. The integration and functional tests share one fixture for each assembly, which `AssemblyHooks.cs` starts and stops. Each test must create its own data and must not depend on rows from other tests. If a test reads state of the whole process, such as all exported spans, put `[NotInParallel]` on its class.

To run the tests in a web UI, open the **Labcoat UI** link of the `tests` resource in the Aspire dashboard. [Labcoat](https://github.com/eriklieben/labcoat) builds and runs the test projects, and shows each failure with its source.

To use Labcoat without the stack, run `dotnet labcoat` from the repository root.

Local runs reuse one Postgres container and create a new database for each run. If the `CI` variable is set, each run gets a new container.
