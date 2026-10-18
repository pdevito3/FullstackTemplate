# Features and CQRS Patterns

Guidelines for implementing vertical slice features using MediatR following the Command Query Responsibility Segregation (CQRS) pattern.

## Directory Structure

Features are organized within each domain entity folder:

```
Domain/
└── [EntityName]/
    └── Features/
        ├── Add[EntityName].cs          # Create command
        ├── Update[EntityName].cs       # Update command
        ├── Delete[EntityName].cs       # Delete command
        ├── Get[EntityName].cs          # Single item query
        ├── Get[EntityName]List.cs      # List/paginated query
        └── [BusinessAction].cs         # Domain-specific operations
```

## Command Pattern (Write Operations)

Commands represent state-changing operations: Create, Update, Delete, and business actions.

### Standard Command Structure

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

public static class Add[EntityName]
{
    public sealed record Command([EntityName]ForCreationDto Dto) : IRequest<[EntityName]Dto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, [EntityName]Dto>
    {
        public async Task<[EntityName]Dto> Handle(Command request, CancellationToken cancellationToken)
        {
            var forCreation = request.Dto.To[EntityName]ForCreation();
            var entity = [EntityName].Create(forCreation);

            await dbContext.[EntityName]s.AddAsync(entity, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return entity.To[EntityName]Dto();
        }
    }
}
```

### Update Command Pattern

```csharp
public static class Update[EntityName]
{
    public sealed record Command(string Id, [EntityName]ForUpdateDto Dto) : IRequest<[EntityName]Dto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, [EntityName]Dto>
    {
        public async Task<[EntityName]Dto> Handle(Command request, CancellationToken cancellationToken)
        {
            var entity = await dbContext.[EntityName]s.GetById(request.Id, cancellationToken);

            var forUpdate = request.Dto.To[EntityName]ForUpdate();
            entity.Update(forUpdate);

            await dbContext.SaveChangesAsync(cancellationToken);

            return entity.To[EntityName]Dto();
        }
    }
}
```

### Delete Command Pattern

```csharp
public static class Delete[EntityName]
{
    public sealed record Command(string Id) : IRequest;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command>
    {
        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            var entity = await dbContext.[EntityName]s.GetById(request.Id, cancellationToken);

            dbContext.[EntityName]s.Remove(entity); // Soft delete via DbContext
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
```

### Business Action Command Pattern

For domain-specific operations beyond CRUD:

```csharp
public static class Submit[EntityName]
{
    public sealed record Command(string Id) : IRequest<[EntityName]Dto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, [EntityName]Dto>
    {
        public async Task<[EntityName]Dto> Handle(Command request, CancellationToken cancellationToken)
        {
            var entity = await dbContext.[EntityName]s.GetById(request.Id, cancellationToken);

            entity.Submit(); // Business logic encapsulated in domain entity

            await dbContext.SaveChangesAsync(cancellationToken);

            return entity.To[EntityName]Dto();
        }
    }
}
```

## Query Pattern (Read Operations)

Queries are read-only operations that never modify state.

### Single Item Query

```csharp
public static class Get[EntityName]
{
    public sealed record Query(string Id) : IRequest<[EntityName]Dto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Query, [EntityName]Dto>
    {
        public async Task<[EntityName]Dto> Handle(Query request, CancellationToken cancellationToken)
        {
            var entity = await dbContext.[EntityName]s.GetById(request.Id, cancellationToken);
            return entity.To[EntityName]Dto();
        }
    }
}
```

### List Query with Pagination

```csharp
public static class Get[EntityName]List
{
    public sealed record Query([EntityName]ParametersDto Parameters) : IRequest<PagedList<[EntityName]Dto>>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Query, PagedList<[EntityName]Dto>>
    {
        public async Task<PagedList<[EntityName]Dto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var queryKitConfig = new CustomQueryKitConfiguration();

            // The filter and the sort run on the DTO, so a caller can query only the fields that the contract shows
            var query = dbContext.[EntityName]s
                .AsNoTracking()
                .To[EntityName]DtoQueryable();

            if (!string.IsNullOrWhiteSpace(request.Parameters.Filters))
                query = query.ApplyFilters(request.Parameters.Filters, queryKitConfig);

            // Paging needs a fixed order, or a row can show on two pages or on none
            query = string.IsNullOrWhiteSpace(request.Parameters.SortOrder)
                ? query.OrderBy(e => e.Name).ThenBy(e => e.Id)
                : query.ApplySortOrder(request.Parameters.SortOrder, queryKitConfig).ThenBy(e => e.Id);

            return await PagedList<[EntityName]Dto>.CreateAsync(
                query,
                request.Parameters.PageNumber,
                request.Parameters.PageSize,
                cancellationToken);
        }
    }
}
```

Apply the filter and the sort after the projection to the DTO, not to the entity. On the entity, a caller can filter on a field that the DTO does not show, for example `CreatedBy`. Each DTO field must translate to SQL. If a DTO field comes from a computed entity property, map it with an expression-bodied method, so that Mapperly inlines the expression into the projection (see `UserMapper.MapFullName`).

## Key Principles

### 1. One Feature Per File

Each feature lives in its own file containing both the request and handler:

```csharp
public static class FeatureName
{
    public sealed record Command/Query(...) : IRequest<TResponse>;

    public sealed class Handler(...) : IRequestHandler<Command/Query, TResponse>
    {
        // Implementation
    }
}
```

### 2. Use Static Classes for Feature Grouping

The outer static class groups related request/handler pairs and provides namespace isolation.

### 3. Sealed Records for Requests

Use `sealed record` for immutability and value-based equality:

```csharp
public sealed record Command(string Id, UpdateDto Dto) : IRequest<ResponseDto>;
```

### 4. Sealed Classes for Handlers

Handlers should be sealed to prevent inheritance:

```csharp
public sealed class Handler(...) : IRequestHandler<Command, Response>
```

### 5. Primary Constructor Dependency Injection

Use primary constructors for cleaner DI:

```csharp
public sealed class Handler(
    AppDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<Command, Response>
```

### 6. Always Use CancellationToken

Pass cancellation tokens through to async operations:

```csharp
public async Task<Response> Handle(Command request, CancellationToken cancellationToken)
{
    await dbContext.Entities.AddAsync(entity, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);
}
```

### 7. Commands Save Changes, Queries Don't

- **Commands**: Always call `dbContext.SaveChangesAsync()` at the end
- **Queries**: Never modify data, use `AsNoTracking()` for performance

### 8. Map at Boundaries

- DTOs come in from controllers, convert to domain models
- Domain entities come out from handlers, convert to DTOs
- Use Mapperly for all conversions

## Validation

Use FluentValidation for request validation:

```csharp
public static class Add[EntityName]
{
    public sealed record Command([EntityName]ForCreationDto Dto) : IRequest<[EntityName]Dto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Dto.Name)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.Dto.Email)
                .NotEmpty()
                .EmailAddress();
        }
    }

    public sealed class Handler(...) : IRequestHandler<Command, [EntityName]Dto>
    {
        // ...
    }
}
```

## Anti-Patterns to Avoid

### Don't Put Business Logic in Handlers

```csharp
// Bad - business logic in handler
public async Task<OrderDto> Handle(Command request, CancellationToken ct)
{
    var order = await dbContext.Orders.GetById(request.Id, ct);

    // Business logic should be in the domain entity
    if (order.Status != OrderStatus.Draft)
        throw new ValidationException("Cannot submit non-draft order");
    order.Status = OrderStatus.Submitted;

    await dbContext.SaveChangesAsync(ct);
    return order.ToOrderDto();
}

// Good - delegate to domain entity
public async Task<OrderDto> Handle(Command request, CancellationToken ct)
{
    var order = await dbContext.Orders.GetById(request.Id, ct);

    order.Submit(); // Business logic encapsulated

    await dbContext.SaveChangesAsync(ct);
    return order.ToOrderDto();
}
```

### Don't Return Domain Entities

```csharp
// Bad - exposing domain entity
public sealed record Query(string Id) : IRequest<Order>;

// Good - return DTO
public sealed record Query(string Id) : IRequest<OrderDto>;
```
