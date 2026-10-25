# Features and CQRS

Each feature is one folder in `Domain/[EntityName]s/Features/`, for example `Features/AddUser/`. Its core file, `AddUser.cs`, has a static class with a MediatR request and its handler. The adapter files next to it (`AddUser.Http.cs`, and `.Grpc.cs` or `.Mcp.cs` when the feature has them) send this request. See [endpoints.md](endpoints.md). A command changes state. A query only reads. The examples come from `Domain/Users/Features/`.

## Rules

1. Put the request and the handler of one feature in one core file, in a folder with the same name. Name the folder, the file, and the class for the action, for example `AddUser`, `GetUserList`, or `UpdateUserRole`. Use the namespace `FullstackTemplate.Server.Domain.[EntityName]s.Features` for each file in the folder. A namespace with the feature name would clash with the class name. The exception is `[FeatureName].Grpc.cs`: it uses the namespace of the gRPC service, for example `...Tenants.Grpc.V1`, so that it can extend the partial service class. See [endpoints.md](endpoints.md#grpc).
2. Make the request a `sealed record` named `Command` or `Query`.
3. Make the handler a `sealed class` named `Handler`. Inject services with a primary constructor.
4. Pass the `CancellationToken` to each async call.
5. Tag each EF query with `TagWithOperationCallSite`, and give the feature the telemetry that support needs. See [feature-telemetry.md](feature-telemetry.md).
6. In a command, put the business logic in the entity. The handler loads the entity, calls an entity method, and calls `SaveChangesAsync`.
7. In a query, do not change data. Use `AsNoTracking()` for lists.
8. Return a DTO. Never return an entity.
9. In a command that changes the data of a tenant, make a permission check the first statement of the handler. Do the same in a query that reads data that needs a permission, such as the other users or the agents. See [Permission checks](#permission-checks).

## Validation

No MediatR behavior runs FluentValidation validators. Do not add a `Validator` class to a feature, because it does not run.

- Put the rules for one entity in the entity. See [domain-entities.md](domain-entities.md).
- Put a check that needs the database, such as a unique value, in the handler. Throw a `ConflictException` on the request field, which gives 409. See [backend.md](../backend.md#error-responses).
- Create or change the entity before the check that needs the database, and query with the value of the entity. Then the entity refuses a value that Postgres cannot take, such as a text with U+0000, with a 422 before a query sends it. A query that gets such a value fails, and the API gives 500. When a check must run before the entity exists, such as the lookup of the tenant ID of the request, query only when `EntityId.IsValid` passes for the ID.

## Command

```csharp
public static class UpdateUser
{
    public sealed record Command(string Id, UserForUpdateDto Dto) : IRequest<UserDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Command, UserDto>
    {
        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.RequireSelfOr(request.Id, Permission.ManageUsers());
            // The response carries the record of the user, so it needs the read permission too (ADR 0009).
            caller.RequireSelfOr(request.Id, Permission.ReadUsers());

            var user = await dbContext.Users
                .TagWithOperationCallSite("users.update.load")
                .GetById(request.Id, cancellationToken);

            user.Update(request.Dto.ToUserForUpdate());

            await dbContext.SaveChangesAsync(cancellationToken);

            return user.ToUserDto();
        }
    }
}
```

`GetById` throws a `NotFoundException` when no row has the ID. The API then returns 404. `user.Update` throws a `StaleVersionException` when the DTO has an old version, and `SaveChangesAsync` throws it when another request saved the row after the load. Both give 409. Do not catch it or retry the command, because a retry with the new version overwrites a change that the caller did not see.

A create command calls `Entity.Create`, then `AddAsync`, then `SaveChangesAsync`. A delete command calls `Remove`. The context changes the delete to a soft delete. A command that has no result implements `IRequest`, not `IRequest<T>`.

## List query

A list query projects to the DTO first. Then it applies the QueryKit filter and sort to the DTO. As a result, a caller can filter and sort only on the fields that the contract shows, and not on a field such as `LastModifiedOn`.

Each DTO field must translate to SQL. If a DTO field comes from a computed entity property, map it with an expression-bodied method. Then Mapperly puts the expression into the projection (see `UserMapper.MapFullName`).

`ApplyFilters` and `ApplySortOrder` wrap QueryKit. When the input is not valid, they throw a `QueryInputException` with the name of the parameter, which gives 400.

```csharp
public static class GetUserList
{
    public sealed record Query(UserParametersDto Parameters) : IRequest<PagedList<UserDto>>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Query, PagedList<UserDto>>
    {
        public async Task<PagedList<UserDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            await permissionGuard.RequireAsync(Permission.ReadUsers(), cancellationToken);

            var queryKitConfig = new CustomQueryKitConfiguration();

            // The filter and the sort run on the DTO, so a caller can query only the fields that the contract shows
            var query = dbContext.Users
                .TagWithOperationCallSite("users.list")
                .AsNoTracking()
                .ToUserDtoQueryable();

            if (!string.IsNullOrWhiteSpace(request.Parameters.Filters))
                query = query.ApplyFilters(request.Parameters.Filters, queryKitConfig);

            // Paging needs a fixed order, or a row can show on two pages or on none
            query = string.IsNullOrWhiteSpace(request.Parameters.SortOrder)
                ? query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Id)
                : query.ApplySortOrder(request.Parameters.SortOrder, queryKitConfig).ThenBy(u => u.Id);

            return await PagedList.CreateAsync(
                query,
                request.Parameters.PageNumber,
                request.Parameters.PageSize,
                cancellationToken);
        }
    }
}
```

## Permission checks

A handler checks the permission of the caller with one call to `IPermissionGuard`. The call is the first statement of `Handle`, so a reader of the handler sees the rule. There is no marker interface and no pipeline behavior for access.

```csharp
public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command>
{
    public async Task Handle(Command request, CancellationToken cancellationToken)
    {
        await permissionGuard.RequireAsync(Permission.ManageUsers(), cancellationToken);

        var user = await dbContext.Users
            .TagWithOperationCallSite("users.delete.load")
            .GetById(request.Id, cancellationToken);
        // ...
    }
}
```

`IPermissionGuard` has these methods:

| Method | Passes when |
|--------|-------------|
| `RequireAsync(permission, cancellationToken)` | The caller has the permission |
| `RequireSelfOrAsync(userId, permission, cancellationToken)` | The caller is the user with `userId`, or the caller has the permission |

`GetCallerAsync(cancellationToken)` does not throw. It gives the `Caller`, with `Permissions`, a read-only set of each permission that the caller has, and `Has(permission)`. Use it in two cases:

- A rule needs the set, for example an entity method that lets only a caller with a given permission lower a critical defect. Give `caller.Permissions` to the entity method.
- The handler makes more than one check. Load the `Caller` once, then call `caller.Require(permission)` or `caller.Require(permissions)` for each check. `RequireAsync` does the same check, so there is no second rule.

If the check fails, the guard throws `ForbiddenAccessException`. HTTP returns 403 with problem details, gRPC returns `PermissionDenied`, and MCP returns a `permission_denied` tool error. A request with no valid token gets 401 before the handler runs. Declare the 403 on the endpoint with `.ProducesProblem(StatusCodes.Status403Forbidden)`.

Obey these rules:

1. Put the check before the first load. Then a caller without the permission gets 403 for each ID, and does not learn which IDs exist. The exception is a self check that needs the ID from the load, as in `GetUserByIdentifier`. Then also check the permission when the load finds no row. Thus a caller without the permission gets 403 also for a value that does not exist.
2. Do not throw `ForbiddenAccessException` for a missing permission yourself, and do not read `User.Role` for access. The role gives only the default permissions of a user.
3. A caller can give or take away only the permissions that it has. When a command changes the permissions of a user, also require each permission that it gives or takes away. `AddUser` gives the default permissions of the role. `AddUserPermission` and `RemoveUserPermission` give or take away one permission. `UpdateUserRole` takes away each permission of the user and gives the defaults of the new role. `DeleteUser` takes away each permission of the user.
4. A query checks a read permission when it reads data that needs one, for example the other users (`read_users`), the agents (`read_agents`), or the audit log (`read_audit_log`). A query whose rows each member can see, such as the tenant of the caller, has no check. In both cases, the tenant filter still limits the rows that the query reads. A command that returns the record of another user or of an agent also checks the read permission, because its response shows the same data as the query. For example, `RemoveUserPermission` requires `read_users`, and `AddAgentPermission` requires `read_agents`.
5. Do not add a permission check to an adapter. The HTTP endpoint, the gRPC method, and the MCP tool send the same request, so the check in the handler applies to each of them.

For the permissions, the callers that pass, and how to add a permission, see [backend.md](../backend.md#permissions).
