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

Keep each C# file at 1000 lines or less. This rule covers all C# files in the solution, not only the feature files. Split a larger file by part, as a feature puts its handler and each entrypoint in their own files. This rule does not apply to generated files, such as the EF migrations and the model snapshot.

## Main concepts

- **Rich domain entities** have private setters, factory methods, and their own business logic.
- **CQRS with MediatR**: commands do the writes, and queries do the reads.
- **Vertical slices**: each feature is one folder. Its core file has the request and the handler. Its adapter files expose the feature over HTTP, gRPC, and MCP.
- **Mapperly** generates the DTO mapping code at compile time.
- **QueryKit** does the filters and sorts for list endpoints. They run on the DTO projection, so a caller can query only the fields that the contract shows. `CustomQueryKitConfiguration` limits the length and the nesting depth of a filter.
- **Value objects** (`EmailAddress`, `UserRole`, and more) use SmartEnum when they have fixed options.
- **Domain events** queue in the entity. `SaveChangesAsync` runs their handlers in the save transaction. See [domain-events.md](backend/domain-events.md).
- **Audit log**: each save writes an audit entry for each data change and each recorded action, in the save transaction. See [audit-log.md](backend/audit-log.md).
- **Prefixed string IDs**: each entity has a `public const string IdPrefix`, for example `usr`. `EntityId.New` makes IDs like `usr_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f` (the prefix and a UUIDv7). The build gives an error when an entity has no prefix, an invalid prefix, or a prefix that another entity uses. It also gives an error when a constructor does not call `base(IdPrefix)` with the prefix of its own type, or when an entity derives from another entity. A UUIDv7 ID shows the creation time to the millisecond, so the creation time of a record is not secret.

## Add a new feature

For a new entity that a tenant owns, run `scripts/new-feature.cs`. It writes the entity, its configuration and migration, the DTOs, the add, get, list, update, and delete features, the entrypoints, the permission, and the tests. Then write the domain rules. The `backend-feature` skill gives the command and the steps after it. The `new-feature` job of CI runs the script for two samples, then builds and tests the result, so a change to a rule that breaks the output of the script fails CI.

For another change, do these steps in sequence. Each step has a doc in `docs/backend/`.

1. Create the domain entity ([domain-entities.md](backend/domain-entities.md)).
2. Add the entity configuration, then add a migration ([entity-configurations.md](backend/entity-configurations.md)).
3. Create the DTOs and the Mapperly mapper ([dtos-and-mappings.md](backend/dtos-and-mappings.md)).
4. Add the commands and queries ([features-and-cqrs.md](backend/features-and-cqrs.md)). Give each command a permission check. Also give a permission check to each query that reads data that needs a permission. If no permission fits, add one (see [Permissions](#permissions)).
5. Add the endpoint, and the gRPC method and the MCP tool if the feature needs them ([endpoints.md](backend/endpoints.md)).
6. Add the domain events and their handlers ([domain-events.md](backend/domain-events.md)).
7. Add the telemetry that production support needs: query tags, id tags on the feature span, logs, and metrics ([feature-telemetry.md](backend/feature-telemetry.md)).
8. Regenerate the API contract with the `api-contract` skill (see [OpenAPI document](#openapi-document)).

## OpenAPI document

`Microsoft.AspNetCore.OpenApi` makes the OpenAPI document. Swagger UI shows it at `/swagger`. The Server maps `/swagger` and `/openapi/v1.json` only in Development. A Debug build also writes the document to `openapi/FullstackTemplate.Server.json`. Other configurations do not write it, because the Server csproj sets `OpenApiGenerateDocuments` to false for them. The frontend generates its API contract from this file (see [frontend.md](frontend.md#api-contract)).

The build starts the host without an auth provider or a database. `Resources/Entrypoints/Http/OpenApi/OpenApiBuildTime.cs` gives the host placeholder auth values for this run. This run does no OIDC discovery, so the committed file has no OAuth2 security scheme, but the runtime document at `/openapi/v1.json` has it. If startup code needs an external system, skip it when `OpenApiBuildTime.IsActive` is true.

The document transformers in `Resources/Entrypoints/Http/OpenApi/` set these rules:

- Each endpoint must have a name (`.WithName`). If an endpoint has no name, the Debug build fails.
- A property that is not nullable is required. A nullable property is optional, also when it refers to another DTO, such as `AddressDto? ShipTo`.
- A `PagedList<T>` response documents the `X-Pagination` header with the `PaginationMetadata` schema. `PaginationHeaderFilter` sets this header on each `PagedList<T>` result. It is an endpoint filter on the versioned group, so each endpoint that `EndpointDiscovery` maps has it.
- An endpoint that returns a `PagedList<T>` must have a name that ends in `List`. Other endpoints must not have a name that ends in `List`. If not, the Debug build fails. The frontend list clients use this name to return the full response.
- An endpoint that requires authentication documents a 401 with no body. An endpoint with `[AllowAnonymous]` or no authorization metadata gets no 401. The endpoints have no authorization policies, so the transformer cannot know which endpoints return 403. A 403 comes from the handler (see [Permissions](#permissions)). Declare a 403 with `.ProducesProblem(StatusCodes.Status403Forbidden)` on each endpoint whose handler can throw `ForbiddenAccessException`.
- Requests and success responses use only `application/json`. Error responses with a `ProblemDetails` body use only `application/problem+json`, as the Server sends them. The document types numbers only as numbers. The Server still accepts the lenient forms (`text/json` and `application/*+json` request bodies, and numbers in JSON strings), but the contract states only the canonical form.
- A DTO property with `[NamesOf<T>]` lists the names of `T` as an enum. For a list of names, the items have the enum. For any other schema, such as a nullable string, the Debug build fails. The Server still reads a name in any case, for example `admin` for `Admin`, but the contract states only the exact name.
- A request DTO property with `[SchemaMaxLength]`, `[SchemaMinimum]`, or `[SchemaMaximum]` gets the value of the named domain constant as its `maxLength`, `minimum`, or `maximum`. The attribute names a `const` or a `static readonly` field, for example `[SchemaMaxLength(typeof(User), nameof(User.FirstNameMaxLength))]`. A max length on a schema that is not a string, or a minimum or a maximum on a schema that is not a number, fails the Debug build. It is not a request rule. Do not use `[Range]` or `[MaxLength]` for a domain limit, because `RequestValidationBehavior` runs them and gives a 400 before the domain rule gives its 422.

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

All error responses use RFC 7807 problem details. The text of each message follows [messages.md](messages.md). `Exceptions/DomainError.cs` classifies the domain exceptions, and `Resources/Entrypoints/DomainProblemDetails.cs` maps them to HTTP. gRPC and MCP map the same classes (see [endpoints.md](backend/endpoints.md)):

| Exception | Status |
|-----------|--------|
| `ValidationException`, `FluentValidation.ValidationException` | 422. `detail` has the message. `errors` has the messages for each named property |
| `NotFoundException` | 404 |
| `ForbiddenAccessException` | 403. `detail` has the message |
| `ConflictException` from a duplicate check in a handler, and `ConstraintConflictException`, which `AppDbContext` throws when a unique index (Postgres `23505`) or an exclusion constraint (`23P01`) stops a save | 409 with the default `type`. `title` is "The request conflicts with a record that exists." `detail` has the message, and `errors` has it under the named field. A `ConstraintConflictException` has the field and the message of its constraint. It does not show the constraint or the values |
| `StaleVersionException`, which `GuardVersion` throws for an old version in the request, and `AppDbContext` throws when another save changed the row after the load, or when a unique index with `ForStaleVersion` stops a save | 409 with `type` `urn:fullstacktemplate:problem:stale-version`. `title` is "The record changed after you loaded it." `detail` has the message, and `errors` has it under `version` |
| `ConcurrentChangeException`, which `RaceRetryBehavior` throws when a command with no version lost the race on each run | 409 with `type` `urn:fullstacktemplate:problem:concurrent-change`. `title` is "Other users changed the record at the same time." `detail` has the message, and `errors` is empty |
| `RequestValidationException`, from a DataAnnotations rule in `RequestValidationBehavior` | 400. `errors` has the messages for each camelCase field |
| `QueryInputException`, for a query value that the Server cannot apply: a `filters` or `sortOrder` value that QueryKit cannot apply, a value with a character that Postgres cannot take, or a malformed `cursor` | 400. `errors` has the message under the name of the parameter. For a `filters` or `sortOrder` value, `QueryKitInputExtensions` writes a message that tells the caller what to change: the character where the parse failed, the field that the list does not have with the fields that it has, the field that a value does not fit, the limit that the filter broke, or the sounds-like operators that the list cannot run. The message never holds the value of a comparison. The feature slice span gets the type of the exception, with the place of a parse fault or the limit that the filter broke. The span does not get the text of the filter or a field name, because a caller can type any text as a field name |
| Any other exception | 500 |

If a request value does not bind, or a DataAnnotations rule fails, the Server returns 400 with a `ValidationProblemDetails` body. This body has the `errors` field, keyed by the camelCase name of each field. Declare `.ProducesValidationProblem()` on each endpoint that can return 400, so that the OpenAPI document states this shape. This includes each list endpoint, because of the QueryKit row in the table.

A 422 comes from a domain rule or a FluentValidation rule. Its body also has the `ValidationProblemDetails` shape. Declare `.ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)` on each write endpoint (POST, PUT, PATCH, and DELETE). Also declare it when the endpoint has no request body, because a domain rule can fail in any write.

A 409 comes from a duplicate unique value, an overlap that an exclusion constraint stops, or a record that changed after the caller loaded it. Its body also has the `ValidationProblemDetails` shape. The `type` tells the cases apart. The type `urn:fullstacktemplate:problem:stale-version` (`DomainProblemDetails.StaleVersionType`) means that the caller must load the record again and repeat the change. The type `urn:fullstacktemplate:problem:concurrent-change` (`DomainProblemDetails.ConcurrentChangeType`) means that the caller can send the same request again. Any other type means that the caller must change the value of the field in `errors`.

Each entrypoint shows the three kinds differently:

| Kind | HTTP | gRPC | MCP `code` |
|------|------|------|------------|
| Conflict with a record that exists | 409 with the default `type` | `AlreadyExists` | `already_exists` |
| Stale version | 409 with the stale version `type` | `Aborted` | `aborted` |
| Concurrent change | 409 with the concurrent change `type` | `Aborted` | `aborted` |

Declare `.ProducesValidationProblem(StatusCodes.Status409Conflict)` on each write endpoint that changes an existing record, because each one can lose a race with another save. Also declare it on each write endpoint that can add or change a unique value.

In a list handler, call `ApplyFilters` and `ApplySortOrder` from `Resources/QueryKitInputExtensions.cs`, not the QueryKit methods. They throw a `QueryInputException` with the name of the parameter. If an operator does not fit the type of a property, QueryKit fails with a .NET exception. Then the response gets a fixed message, and the trace gets the exception.

Do not catch these exceptions in an endpoint. Throw them from the entity or the handler.

Before you add a row with a unique value, check for the value in the handler and throw a `ConflictException` on the property. The check gives a clear message. The check must count the same rows as the unique index. If the index has a filter, such as `is_deleted = false`, keep the same filter in the check. Two requests can pass the check at the same time, so the unique index stops the second save, and the mapping above gives 409. Give each new unique index its request field and its message with `ForRequestField` in its entity configuration. An index that stops a save only when another user changed the record gets `ForStaleVersion` instead. Give an exclusion constraint, or an index that only a migration adds, its request field and its message in the `NotInModel` dictionary of `ConstraintErrors` (see [entity-configurations.md](backend/entity-configurations.md#unique-indexes)). For a stale-version index that only a migration adds, use `new ConstraintError.StaleVersion()` in place of the request field and the message. Put the message in a constant on the entity, for example `Tenant.NameInUseMessage`, and use the same constant in the check. Then the loser of the race gets the same message as a request that the check stops. A test fails if a constraint has no entry. EF Core 10 cannot index a column of a value object, so add such an index in a migration (see [entity-configurations.md](backend/entity-configurations.md#index-a-column-of-a-value-object)).

## Permissions

A command, or a query that reads data that needs a permission, checks a permission in its handler with `IPermissionGuard` (see [features-and-cqrs.md](backend/features-and-cqrs.md#permission-checks)). A permission is a value of `Permission` in `Domain/Users/Permission.cs`. A user has a permission when they have a `UserPermission` row for it.

| Permission | Handlers that require it |
|------------|--------------------------|
| `manage_users` | `AddUser`, `UpdateUser` for another user, `DeleteUser` |
| `manage_user_access` | `UpdateUserRole`, `AddUserPermission`, `RemoveUserPermission` |
| `manage_tenant` | `UpdateTenant`, `DeleteTenant` |
| `manage_agents` | `AddAgent`, `DeleteAgent`, `AddAgentPermission`, `RemoveAgentPermission` |
| `read_audit_log` | `GetAuditEntryPage` |
| `read_audit_log_sensitive` | None. `GetAuditEntryPage` shows the sensitive values of the audit log only to a caller with it. |
| `read_users` | `GetUserList`, `GetUser` for another user, `GetUserByIdentifier` for another user or for an identifier that no user has. `UpdateUser`, `UpdateUserRole`, `AddUserPermission`, and `RemoveUserPermission` for another user, because they return the user. |
| `read_agents` | `GetAgentList`, `GetAgent`. `AddAgentPermission` and `RemoveAgentPermission`, because they return the agent. |

<!-- example:start -->
The example permission `manage_workshops` is required by `AddVenue`, `AddWorkshop`, `UpdateWorkshop`, `ScheduleWorkshop`, `CancelWorkshop`, and `CompleteWorkshop`. Its migration gives it to the admins that exist: [20261029095936_AddWorkshopsExample.cs](../FullstackTemplate.Server/Databases/Migrations/Examples/20261029095936_AddWorkshopsExample.cs). After the load, the last four commands also check the row: the caller must be the organizer of the workshop, the member who added it, or have `manage_tenant`. A delegated agent is not a member, so it is never the organizer. It changes a workshop only with `manage_tenant`. Thus the example adds no second permission.
<!-- example:end -->

The role of a user gives the default permissions. `UserRole.GetDefaultPermissions` gives each permission to `Admin` and no permission to `User`. `User.Create` and `UpdateRole` write the defaults as rows. After that, `AddUserPermission` and `RemoveUserPermission` change the rows, and the rows decide. The guard does not read the role.

A caller can give or take away a permission only if they have it. Thus a user with only `manage_users` cannot add or delete an `Admin`, and a user with only `manage_user_access` cannot give or take away `manage_tenant` or change the role of an `Admin`. As a result, a user with only some of the permissions cannot take access away from an admin. `manage_tenant` gives both the rename and the delete of the tenant, so give it only to a user who can delete the tenant.

### The callers that pass

| Caller | Result of a permission check |
|--------|------------------------------|
| A user with a user record | Passes if the user has the permission |
| The user that the command is about, for `Caller.RequireSelfOr` or the `unlessOwnRecord` permissions of `UserAction` | Passes. `UserAction.Update` needs `manage_users` and `read_users` only for another user, so that a user can update their own record. `UpdateRole`, `AddPermission`, and `RemovePermission` need `read_users` only for another user. The update DTO has no role, so a user cannot give themselves access. `GetUser` and `GetUserByIdentifier` use the same exception, so that a user can read their own record. |
| A delegated agent | Passes if both its user and its grant have the permission. It does not get the exception of `Caller.RequireSelfOr`. |
| A machine agent | Passes if its grant has the permission |
| A client in `Tenancy:ManageAllTenantsClientIds`, or system work in a system scope | Passes for each permission. The tenant filter still applies. |
| A client in `Tenancy:ReadAllTenantsClientIds` | 403 |
| An unknown client: a client that is not first-party, on no list, and with no agent for the token | 403 |
| A user of a first-party client with no user record, or an anonymous principal | 403. `GetUserByIdentifier` gives a user with no user record and no tenant 404 when it finds no user, because that user sees no rows. Thus a new user who reads their own identifier knows to start a tenant. |

[Agents](authentication.md#agents) tells how the server identifies an agent and how a tenant registers it.

`AddTenant` and `InitiateUserAndTenant` act outside one tenant. They do not check a permission. They check in the handler that the caller can manage all tenants (see [multi-tenancy.md](multi-tenancy.md#row-scope-and-capability)). Do not make this capability a permission, because an admin could then give it to themselves.

### Where the permissions come from

The guard reads the user and their permission rows from the database in each handler that checks. It does not cache them and does not read them from the token. As a result, a removed permission or a deleted user is denied on the next request on each server instance. The cost is one indexed query in each handler that checks. The reasons are in [ADR 0009](adr/0009-permission-checks-in-the-handler.md).

### Add a permission

`scripts/new-feature.cs` does steps 1, 2, and 6 for the permission of a new entity. For another permission:

1. Add a constant to `Permission.Names` in `Permission.cs`, a value of `PermissionEnum` with that constant as its name, and a static method that returns it, for example `Permission.ManageOrders()`. Use a snake case name, for example `manage_orders`.
2. Add an EF Core migration that gives the permission to the admins that exist (see [Migrations](#migrations)). Call `migrationBuilder.GrantPermissionToRole("Admin", "manage_orders")` in `Up`. Write the name as a string, so that the migration does not change when `Permission` changes. New admins get the permission from the role defaults. If the users with another permission also need the new one, call `GrantPermissionToHoldersOf` too, as `GrantReadPermissions` does.
3. Call `permissionGuard.RequireAsync(Permission.ManageOrders(), cancellationToken)` first in each handler that needs it.
4. Declare the 403 on each HTTP endpoint of these handlers. Give each MCP tool of these handlers `[ToolAccess(Permission.Names.ManageOrders)]` (see [endpoints.md](backend/endpoints.md#tool-access)).
5. Add an integration test for a user with the permission and a user without it.
6. Add the name of the permission to `AllNames` in `PermissionTests`. The list names all the permissions, so `list_names_returns_all_permission_names` and `get_all_returns_all_permissions` fail until you add it.

To remove a permission, remove its value and add a migration that calls `DeletePermission`. A row with a name that `Permission` does not know stops each load of its user.

## Database

`Databases/AppDbContext.cs` has these features:

- **Soft delete**: the context filters out entities with `IsDeleted` automatically.
- **Last modified**: the context sets `LastModifiedOn` on each row that a save writes. The audit log records who made each change and when. `LastModifiedOn` stays because a save that changes an owned child writes it on the owner, so the owner row and its version change (see [Concurrency](#concurrency)).
- **Tenant filter**: see [multi-tenancy.md](multi-tenancy.md).
- **Domain events**: the context runs the handlers before it sends the SQL, so the handler changes commit with the save.
- **Audit log**: the context adds the audit entries of the save before it sends the SQL. Only the save and `BulkInsertAsync` can write an audited table. See [audit-log.md](backend/audit-log.md).
- **Optimistic concurrency**: each update checks the version of the row. See [Concurrency](#concurrency).

Entity configurations are in `Databases/EntityConfigurations/`. Database names use snake case.

The context adds the soft delete filter, the `xmin` version, the ID field mapping, and the tenant filter with EF Core conventions. Thus a context that configures an entity after `base.OnModelCreating`, for example in a test, also gets them for that entity.

A paged list query must have a fixed sort order. Without one, a row can show on two pages or on no page. When the caller gives no sort, sort by a natural key and then by `Id`. After a custom sort, also sort by `Id`. Add an index on the default sort, so that the database can read the page in order. See `GetUserList.cs` and `UserConfiguration.cs` for an example.

### Concurrency

Each command picks a version, a run again, or a lock for a race with another request. See [concurrency.md](backend/concurrency.md).

### Migrations

```bash
dotnet ef migrations add MyMigration --project FullstackTemplate.Server --startup-project FullstackTemplate.Server --output-dir Databases/Migrations
dotnet ef database update --project FullstackTemplate.Server --startup-project FullstackTemplate.Server
```

The server applies migrations at startup, with a Postgres advisory lock.

`ModelSnapshotTests` fails when the committed model snapshot is not the snapshot that the next migration writes. This includes a model change with no DDL, such as a new navigation, which `dotnet ef migrations has-pending-model-changes` does not find. The test ignores the `using` lines and the `ProductVersion` annotation. To correct the failure, add a migration in the change that changed the model. For a change with no DDL, the `Up` and `Down` of the migration are empty. An update of EF Core or Npgsql that changes the generated code also fails the test. Correct it the same way.

## Tests

| Project | Scope |
|---------|-------|
| `tests/FullstackTemplate.UnitTests` | Domain logic and small units, with no database |
| `tests/FullstackTemplate.IntegrationTests` | Feature handlers against a real PostgreSQL container |
| `tests/FullstackTemplate.FunctionalTests` | HTTP, gRPC, and MCP endpoints against one shared app and database |
| `tests/FullstackTemplate.SharedTestHelpers` | Fakes and builders for all test projects |

The tests use [TUnit](https://tunit.dev) on Microsoft.Testing.Platform, with Shouldly for assertions. Write `[Test]` for each test, `[Arguments(...)]` for an inline case, and `[MethodDataSource(nameof(...))]` for cases from a method or property.

Mark the parts of each test with `// Arrange`, `// Act`, and `// Assert`, in this order. Put each marker on the line before the first statement of its part. The check reads the order of the markers, and that no statement comes before the first marker. It does not read where a part ends. A test with no setup can start at `// Act`. One line can join two parts with `&` or `and`, as in `// Act & Assert` or `// Arrange and act`. Use `// Act & Assert` when the assertion runs the act, as in `Subject.Compute(x).ShouldBe(y)` or `Should.ThrowAsync`. A test with no act step, such as a check of a constant, a type, or a default, puts `// Act & Assert` on its assertion, because the assertion reads the value. A note can follow the marker, as in `// Act - the second save`. A test has a block body, because an expression body cannot hold the markers. Check: `TestMarkerTests` reads each `[Test]` in `tests/` and fails on each test that breaks the rule.

The functional tests share one `TestingWebApplicationFactory`. Each test gets its own `FactoryClient`.

A test never takes today from the UTC date. Take it from the zone of the place in the data, with `ZonedDate.TodayAt(zone, ctx.Now)`. From 00:00 UTC to local midnight in the Americas, the UTC date is one day after the local date. A test that takes today from the UTC date then fails for some hours each day, for example when the domain refuses its date as in the future. The unit, integration, functional, and shared helper projects ban `DateTime.Today`, `DateTime.Now`, `DateTimeOffset.Now`, and `DateOnly.FromDateTime` in `tests/BannedSymbols.txt` (RS0030). The Bff tests do not get the ban, because they cannot see `ZonedDate` and use a fake `TimeProvider`. For a fixed date, use `new DateOnly(year, month, day)`. For the date of a fixed instant, use `zone.ToLocal(instant).Date`. Suppress RS0030 only when neither fits, and give a reason.

TUnit runs all tests in parallel, also the tests of one class. The integration and functional tests share one fixture for each assembly, which `AssemblyHooks.cs` starts and stops. Each test must create its own data and must not depend on rows from other tests. If a test reads state of the whole process, such as all exported spans, put `[NotInParallel]` on its class.

A list with a search has an integration test that searches for `50%`, `a_b`, and `c\d`, and finds only the row with that text. See `tests/FullstackTemplate.IntegrationTests/Databases/LikePatternTests.cs`.

To run the tests in a web UI, open the **Labcoat UI** link of the `tests` resource in the Aspire dashboard. [Labcoat](https://github.com/eriklieben/labcoat) builds and runs the test projects, and shows each failure with its source.

To use Labcoat without the stack, run `dotnet labcoat` from the repository root.

Local runs reuse one Postgres container and create a new database for each run. If the `CI` variable is set, each run gets a new container.

### Race tests

A race test puts two requests in one fixed order. Write a race test for each of these:

- A command that sends no version and runs again after it loses a race (see [concurrency.md](backend/concurrency.md#run-again)).
- A command that takes a lock.
- A rule that counts or checks other rows, such as open appointments, stock, or overlaps.
- A unique value that two requests can add at the same time.

Use `PauseAtQuery` from `tests/FullstackTemplate.SharedTestHelpers/Utilities/`. It holds the first command whose SQL has a query tag, the name that `TagWithOperationCallSite` gives the query. Find the tag in the `TagWithOperationCallSite` call of the handler. As a result, the test names a step of the handler, not a row.

1. Pause the request under test after the tag of the load that the rival must follow. Use `PauseAtQuery.After(tag)`. `PauseAtQuery.Before(tag)` holds the command before it runs.
2. Send the request under test through `pause.BuildServices()`, from `PauseAtQueryServices.cs` of the integration tests. Send the rival through a usual `TestingServiceScope`. Set the caller on each scope with `SetUserWithDefaults`.
3. Wait for `pause.ReachedAsync()`, then commit the rival. If the rival must wait on a lock of the request under test, start the rival and wait for `pause.WaitUntilBlockedAsync(rival)`.
4. Call `pause.Release()`. Then assert the result of each request, and the end state of each row that either request writes.

```csharp
var pause = PauseAtQuery.After("tenants.update.load");
await using var services = pause.BuildServices();
var underTest = new TestingServiceScope(services);
var rival = new TestingServiceScope();
underTest.SetUserWithDefaults(identifier: admin.Identifier);
rival.SetUserWithDefaults(identifier: admin.Identifier);

var update = underTest.SendAsync(new UpdateTenant.Command(tenant.Id,
    new TenantForUpdateDto { Name = "Northwind", Version = tenant.Version }));
await pause.ReachedAsync();
await rival.SendAsync(new UpdateTenant.Command(tenant.Id,
    new TenantForUpdateDto { Name = "Contoso", Version = tenant.Version }));
pause.Release();
await Should.ThrowAsync<StaleVersionException>(update);
```

Do not use `Task.WhenAll` alone for a race test. The order of two parallel requests comes from the scheduler, so the test can pass when the fix is gone.

Each wait of `PauseAtQuery` fails after 30 seconds with a message that names the tag. As a result, a test that does not get to the step fails, and the run does not hang.

Make the fix fail one time on purpose. Remove the run again, the lock, or the version check, and make sure that the race test fails. Then put the fix back. See `ConcurrencyTests.cs` and `RunAgainWhenRacedTests.cs` in `tests/FullstackTemplate.IntegrationTests/Databases/` for examples.
