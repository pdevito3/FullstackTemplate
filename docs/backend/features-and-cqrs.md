# Features and CQRS

Each feature is one folder in `Domain/[EntityName]s/Features/`, for example `Features/AddUser/`. Its core file, `AddUser.cs`, has a static class with a MediatR request and its handler. The adapter files next to it (`AddUser.Http.cs`, and `.Grpc.cs` or `.Mcp.cs` when the feature has them) send this request. See [endpoints.md](endpoints.md). A command changes state. A query only reads. The examples come from `Domain/Users/Features/`.

## Rules

1. Put the request and the handler of one feature in one core file, in a folder with the same name. Name the folder, the file, and the class for the action, for example `AddUser`, `GetUserList`, or `UpdateUserRole`. Use the namespace `FullstackTemplate.Server.Domain.[EntityName]s.Features` for each file in the folder. A namespace with the feature name would clash with the class name. The exception is `[FeatureName].Grpc.cs`: it uses the namespace of the gRPC service, for example `...Tenants.Grpc.V1`, so that it can extend the partial service class. See [endpoints.md](endpoints.md#grpc).
2. Make the request a `sealed record` named `Command` or `Query`.
3. Make the handler a `sealed class` named `Handler`. Inject services with a primary constructor.
4. Pass the `CancellationToken` to each async call.
5. Tag each EF query with `TagWithOperationCallSite`, and give the feature the telemetry that support needs. See [feature-telemetry.md](feature-telemetry.md).
6. In a command, put the business logic in the entity. The handler loads the entity, calls an entity method, and calls `SaveChangesAsync`. When one rule set has two callers, or a rule reads more than one aggregate, see [One rule set for two callers](#one-rule-set-for-two-callers).
7. In a query, do not change data. Use `AsNoTracking()` for lists.
8. Return a DTO. Never return an entity.
9. In a command that changes the data of a tenant, make a permission check the first statement of the handler. Do the same in a query that reads data that needs a permission, such as the other users or the agents. See [Permission checks](#permission-checks).

## Validation

No MediatR behavior runs FluentValidation validators. Do not add a `Validator` class to a feature, because it does not run.

- Put the rules for one entity in the entity. See [domain-entities.md](domain-entities.md).
- Put a check that needs the database, such as a unique value, in the handler. Throw a `ConflictException` on the request field, which gives 409. See [backend.md](../backend.md#error-responses).
- Create or change the entity before the check that needs the database, and query with the value of the entity. Then the entity refuses a value that Postgres cannot take, such as a text with U+0000, with a 422 before a query sends it. A query that gets such a value fails, and the API gives 500. When a check must run before the entity exists, such as the lookup of the tenant ID of the request, query only when `EntityId.IsValid` passes for the ID. A read value that goes to a query without an entity, such as a filter value, must pass `PostgresText.CanStore`. If it fails, throw a `QueryInputException` with the name of the parameter.

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

`GetById` throws a `NotFoundException` when no row has the ID. The API then returns 404. An ID that `EntityId.IsValid` refuses for the entity, such as an ID with U+0000, gets the same exception with no query. `user.Update` throws a `StaleVersionException` when the DTO has an old version, and `SaveChangesAsync` throws it when another request saved the row after the load. Both give 409. Do not catch it or retry the command, because a retry with the new version overwrites a change that the caller did not see.

A create command calls `Entity.Create`, then `AddAsync`, then `SaveChangesAsync`. A delete command calls `Remove`. The context changes the delete to a soft delete. A command that has no result implements `IRequest`, not `IRequest<T>`.

## One rule set for two callers

Rule 6 puts a rule in the entity. That holds for a rule that reads only its own entity. When one rule set has two callers, use the decision shape. An example is a list of the free days and the command that books one day. When a rule reads facts from more than one aggregate, use the shape also. After the permission check, the list handler does one load and one decision for each candidate. The command handler does one load, one decision, and one save.

The shape has five parts:

1. **A case record.** It holds each fact that the rules read. One loader fills it, and both callers use that loader. Its constructor checks only that the facts belong together, for example that each booking is for the room.
2. **An ordered list of rules** in one static class. Each rule takes the case and returns a refusal, or null when the rule passes. `Decide` returns the first refusal. The order is part of the contract, because the SPA and the tests read the first error.
3. **A closed refusal type.** Each kind of refusal is a sealed record with the field and the message. The private constructor of the base record stops a plain derived record. C# keeps the copy constructor of a record protected, so a test checks that each kind is nested in the base record. The handler maps each kind to an exception. `Taken` gives a `ConflictException` (409), and `NotOffered` gives a `ValidationException` (422). The rules do not reference `Exceptions`.
4. **A proof type** that only `Decide` makes. Its constructor is `internal`, and in the Server only `Decide` calls it. For a private constructor, put `Decide` in the proof type. The write method takes the proof, so the write cannot run without the check.
5. **Two callers, one decision.** The list calls `Decide` for each candidate and keeps the offered ones. The command calls `Decide` one time and throws the refusal.

```csharp
public sealed record Room(string Id, int DaysAhead);

public sealed class Booking
{
    private Booking(string roomId, DateOnly day) => (RoomId, Day) = (roomId, day);

    public string RoomId { get; }
    public DateOnly Day { get; }

    public static Booking Create(BookableDay day) => new(day.RoomId, day.Day);
}

public sealed record BookingCase
{
    public BookingCase(Room room, IReadOnlyCollection<Booking> bookings, DateOnly today)
    {
        if (bookings.Any(b => b.RoomId != room.Id))
            throw new ArgumentException("Give only the bookings of the room.", nameof(bookings));
        (Room, Bookings, Today) = (room, bookings, today);
    }

    public Room Room { get; }
    public IReadOnlyCollection<Booking> Bookings { get; }
    public DateOnly Today { get; }
}

public abstract record Refusal
{
    private Refusal(string field, string message) => (Field, Message) = (field, message);

    public string Field { get; }
    public string Message { get; }

    public sealed record NotOffered(string Field, string Message) : Refusal(Field, Message);
    public sealed record Taken(string Field, string Message) : Refusal(Field, Message);
}

public sealed class BookableDay
{
    internal BookableDay(string roomId, DateOnly day) => (RoomId, Day) = (roomId, day);

    public string RoomId { get; }
    public DateOnly Day { get; }
}

public abstract record Decision
{
    private Decision() { }

    public sealed record Offered(BookableDay Day) : Decision;
    public sealed record Refused(Refusal Refusal) : Decision;
}

public static class BookingRules
{
    private static readonly Func<BookingCase, DateOnly, Refusal?>[] Rules = [InWindow, Free];

    public static Decision Decide(BookingCase booking, DateOnly day)
        => Rules.Select(rule => rule(booking, day)).FirstOrDefault(r => r is not null) is { } refusal
            ? new Decision.Refused(refusal)
            : new Decision.Offered(new BookableDay(booking.Room.Id, day));

    private static Refusal.NotOffered? InWindow(BookingCase booking, DateOnly day)
        => day < booking.Today || day > booking.Today.AddDays(booking.Room.DaysAhead)
            ? new Refusal.NotOffered("day", $"Pick a day from today to {booking.Room.DaysAhead} days ahead.")
            : null;

    private static Refusal.Taken? Free(BookingCase booking, DateOnly day)
        => booking.Bookings.Any(b => b.Day == day)
            ? new Refusal.Taken("day", "Someone booked this day. Pick another day.")
            : null;
}

public static class BookingHandlers
{
    public static IReadOnlyList<DateOnly> FreeDays(BookingCase booking, IEnumerable<DateOnly> candidates)
        => candidates.Where(day => BookingRules.Decide(booking, day) is Decision.Offered).ToList();

    public static Booking Book(BookingCase booking, DateOnly day) => BookingRules.Decide(booking, day) switch
    {
        Decision.Offered offered => Booking.Create(offered.Day),
        Decision.Refused { Refusal: Refusal.Taken taken } => throw new ConflictException(taken.Field, taken.Message),
        Decision.Refused { Refusal: Refusal.NotOffered notOffered }
            => throw new ValidationException(notOffered.Field, notOffered.Message),
        _ => throw new UnreachableException(),
    };
}
```

`FreeDays` and `Book` are the bodies of the two handlers after the load. Each handler calls the same loader, for example an extension method `LoadBookingCaseAsync(roomId, cancellationToken)` on `AppDbContext`. The command handler then adds the booking and calls `SaveChangesAsync` one time. C# cannot see that `Decision` is closed, so the switch needs the `_` arm. A new kind of refusal gets its own arm, and until it has one, the command throws `UnreachableException`. Write the refusal kind as a span tag or a metric tag. See [feature-telemetry.md](feature-telemetry.md).

The case holds the facts at the time of the load. Two requests can pass `Free` at the same time and both save. Back a rule such as `Free` with a unique index or an exclusion constraint, so that the second save gives 409. See [backend.md](../backend.md#error-responses).

A rule that reads only its own entity stays a guard in the entity. See [domain-entities.md](domain-entities.md#guards). Do not add an `IRule` interface, a rule registry, or DI for the rules. One list in one file shows the order.

Each use of the shape has four tests. `tests/FullstackTemplate.UnitTests/Docs/DecisionShapeExampleTests.cs` has this example and its tests:

- A table-driven unit test for each rule. Each row gives the case, the kind of refusal, the field, and the message.
- An order test. A case that breaks each rule gets the refusal of the first rule. Make the case pass the rules one at a time, and the refusal of the next rule comes back.
- A test that ties the callers together. The command accepts each candidate that the list offers. In an app, make this an integration test, because it also tests the loader.
- A test that each kind of the refusal type and of the decision type is nested in its base record. C# cannot close a record hierarchy, so this test does it.

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

### Search text

A free-text search of a list uses `EF.Functions.ILike(column, LikePattern.Containing(search), LikePattern.EscapeCharacter)`. A name check that ignores case uses `LikePattern.Exactly`, and a prefix match uses `LikePattern.StartingWith`. `LikePattern` escapes `%`, `_`, and `\`, so a search for `50%` or `a_b` finds only the rows with that text. A name check gives a clear error, but only a unique index on the name stops two requests that save the same name at one time.

`BannedSymbols.txt` bans the two-argument `ILike` and `Like` (RS0030). They send `ESCAPE ''`, so the backslash of an escaped pattern escapes nothing.

Trim the search, and give it a max length on the parameters DTO. If the search is empty after the trim, do not apply it. If the search fails `PostgresText.CanStore`, throw a `QueryInputException` with the name of the parameter. See [Validation](#validation). Apply the search before `ApplyFilters`, on the entity or on the DTO projection. The query keeps its fixed sort.

A QueryKit filter needs no `LikePattern`. EF Core escapes the pattern of its contains, starts-with, and ends-with operators.

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
