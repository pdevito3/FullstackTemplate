# Backend

`FullstackTemplate.Server` is the backend API. It uses .NET 10, CQRS with MediatR, and EF Core on PostgreSQL. The detailed rules for each pattern are in `.claude/rules/backend/`.

## Domain structure

Each domain entity has this folder structure:

```
FullstackTemplate.Server/Domain/
└── [EntityName]s/
    ├── [EntityName].cs              # Domain entity
    ├── Controllers/v1/              # API controllers
    ├── Dtos/                        # DTOs (read/write)
    ├── Features/                    # CQRS commands/queries
    ├── Mappings/                    # Mapperly mappers
    ├── Models/                      # Internal models
    └── DomainEvents/                # Domain events
```

## Main concepts

- **Rich domain entities** have private setters, factory methods, and their own business logic.
- **CQRS with MediatR**: commands do the writes, and queries do the reads.
- **Vertical slices**: each feature is one file with its request, validator, and handler.
- **Mapperly** generates the DTO mapping code at compile time.
- **QueryKit** does the filters and sorts for list endpoints.
- **Value objects** (`EmailAddress`, `UserRole`, and more) use SmartEnum when they have fixed options.
- **Domain events** queue in the entity. `SaveChangesAsync` dispatches them.

## Add a new feature

Do these steps in sequence. Each step has a rule file in `.claude/rules/backend/`.

1. Create the domain entity (`working-with-domain-entities.md`).
2. Add the entity configuration, then add a migration (`entity-configurations.md`).
3. Create the DTOs and the Mapperly mapper (`dtos-and-mappings.md`).
4. Add the commands and queries, with FluentValidation (`features-and-cqrs.md`).
5. Add the controller (`controllers.md`).
6. Add the domain events and their handlers (`domain-events.md`).
7. Tag each EF query with `TagWithOperationCallSite` (see [telemetry.md](telemetry.md)).
8. Build the Server, then run `pnpm generate:api` in `frontend/` (see [OpenAPI document](#openapi-document)).

## OpenAPI document

`Microsoft.AspNetCore.OpenApi` makes the OpenAPI document. Swagger UI shows it at `/swagger`. A Debug build also writes it to `openapi/FullstackTemplate.Server.json`. The frontend generates its API contract from this file (see [frontend.md](frontend.md#api-contract)).

The build starts the host without an auth provider or a database. `Resources/OpenApiBuildTime.cs` gives the host placeholder auth values for this run. If startup code needs an external system, skip it when `OpenApiBuildTime.IsActive` is true.

The document transformers in `Resources/Extensions/` set these rules:

- Each endpoint must have a name (`Name =` on the HTTP attribute, or `.WithName`). If an endpoint has no name, the build fails.
- A property that is not nullable is required.
- A `PagedList<T>` response documents the `X-Pagination` header with the `PaginationMetadata` schema.
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

The code is in `Resources/Extensions/ApiVersioningExtension.cs`. The URL has the version segment `/api/v{version}/...`.

```csharp
[Route("api/v{v:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public sealed class CustomersController : ControllerBase { }
```

To add a version, add `.HasApiVersion(new ApiVersion(2, 0))` in `GetApiVersionSet()`.

## Error responses

All error responses use RFC 7807 problem details. `Exceptions/DomainProblemDetails.cs` maps the domain exceptions:

| Exception | Status |
|-----------|--------|
| `ValidationException`, `FluentValidation.ValidationException` | 422. `detail` has the message. `errors` has the messages for each named property |
| `NotFoundException` | 404 |
| `ForbiddenAccessException` | 403. `detail` has the message |
| `DbUpdateException` from a unique index (Postgres `23505`) | 422. `detail` does not show the constraint or the values |
| Any other exception | 500 |

Do not catch these exceptions in a controller. Throw them from the entity or the handler.

Before you add a row with a unique value, check for the value in the handler and throw a `ValidationException` on the property. The check gives a clear message. Two requests can pass the check at the same time, so the unique index stops the second save, and the mapping above gives 422.

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

Entity configurations are in `Databases/EntityConfigurations/`. Database names use snake case.

A paged list query must have a fixed sort order. Without one, a row can show on two pages or on no page. When the caller gives no sort, sort by a natural key and then by `Id`. See `GetUserList.cs` for an example.

### Migrations

```bash
dotnet ef migrations add MyMigration --project FullstackTemplate.Server
dotnet ef database update --project FullstackTemplate.Server
```

The server applies migrations at startup, with a Postgres advisory lock.

## Tests

| Project | Scope |
|---------|-------|
| `tests/FullstackTemplate.UnitTests` | Domain logic and small units, with no database |
| `tests/FullstackTemplate.IntegrationTests` | Feature handlers against a real PostgreSQL container |
| `tests/FullstackTemplate.FunctionalTests` | HTTP endpoints against one shared app and database |
| `tests/FullstackTemplate.SharedTestHelpers` | Fakes and builders for all test projects |

The functional tests share one `TestingWebApplicationFactory`. Each test gets its own `FactoryClient`.

All tests in a project run in parallel on one assembly fixture. Each test must create its own data and must not depend on rows from other tests. If a test reads state of the whole app, such as all exported spans, put it in a collection with `DisableParallelization = true`.

Local runs reuse one Postgres container and create a new database for each run. If the `CI` variable is set, each run gets a new container.
