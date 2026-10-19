# Features and CQRS

Each feature is one file in `Domain/[EntityName]s/Features/`. The file has a static class with a MediatR request and its handler. A command changes state. A query only reads. The examples come from `Domain/Users/Features/`.

## Rules

1. Put one feature in one file. Name the file and the class for the action, for example `AddUser`, `GetUserList`, or `UpdateUserRole`.
2. Make the request a `sealed record` named `Command` or `Query`.
3. Make the handler a `sealed class` named `Handler`. Inject services with a primary constructor.
4. Pass the `CancellationToken` to each async call.
5. Tag each EF query with `TagWithOperationCallSite`. See [telemetry.md](../telemetry.md).
6. In a command, put the business logic in the entity. The handler loads the entity, calls an entity method, and calls `SaveChangesAsync`.
7. In a query, do not change data. Use `AsNoTracking()` for lists.
8. Return a DTO. Never return an entity.

## Validation

No MediatR behavior runs FluentValidation validators. Do not add a `Validator` class to a feature, because it does not run.

- Put the rules for one entity in the entity. See [domain-entities.md](domain-entities.md).
- Put a check that needs the database, such as a unique value, in the handler. Throw a `ConflictException` on the request field, which gives 409. See [backend.md](../backend.md#error-responses).

## Command

```csharp
public static class UpdateUser
{
    public sealed record Command(string Id, UserForUpdateDto Dto) : IRequest<UserDto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, UserDto>
    {
        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
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

`GetById` throws a `NotFoundException` when no row has the ID. The API then returns 404.

A create command calls `Entity.Create`, then `AddAsync`, then `SaveChangesAsync`. A delete command calls `Remove`. The context changes the delete to a soft delete. A command that has no result implements `IRequest`, not `IRequest<T>`.

## List query

A list query projects to the DTO first. Then it applies the QueryKit filter and sort to the DTO. As a result, a caller can filter and sort only on the fields that the contract shows, and not on a field such as `CreatedBy`.

Each DTO field must translate to SQL. If a DTO field comes from a computed entity property, map it with an expression-bodied method. Then Mapperly puts the expression into the projection (see `UserMapper.MapFullName`).

`ApplyFilters` and `ApplySortOrder` wrap QueryKit. When the input is not valid, they throw a `QueryInputException` with the name of the parameter, which gives 400.

```csharp
public static class GetUserList
{
    public sealed record Query(UserParametersDto Parameters) : IRequest<PagedList<UserDto>>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Query, PagedList<UserDto>>
    {
        public async Task<PagedList<UserDto>> Handle(Query request, CancellationToken cancellationToken)
        {
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

## Request markers

- `IRequireTenantAdmin`: only a tenant admin can send the command.
- `IAllowSelf`: the user that the command is about can also send it.

See [backend.md](../backend.md#admin-requests).
