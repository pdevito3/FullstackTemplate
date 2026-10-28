---
name: backend-feature
description: Scaffolds backend features including new domain entities, new properties on existing entities, and new business operations. Use when creating a new domain entity, extending an existing entity with new properties, or adding new business features/commands.
allowed-tools: Read, Glob, Grep, Write, Edit, Bash(dotnet:*, ls:*, mkdir:*)
---

# Backend Feature Scaffolding

This skill handles three scenarios:
1. **New Entity** - Create a complete vertical slice for a new domain entity
2. **New Property** - Add properties to an existing entity
3. **New Feature** - Add business operations (commands/queries) to an existing entity

## Scenario 1: New Entity

For a new entity named `[EntityName]` (e.g., `Customer`, `Order`, `Product`), this skill generates:

```
Domain/
└── [EntityName]s/
    ├── [EntityName].cs                 # Rich domain entity
    ├── DomainEvents/
    │   ├── [EntityName]Created.cs
    │   └── [EntityName]Updated.cs
    ├── Dtos/
    │   ├── [EntityName]Dto.cs
    │   ├── [EntityName]ForCreationDto.cs
    │   ├── [EntityName]ForUpdateDto.cs
    │   └── [EntityName]ParametersDto.cs
    ├── Features/                       # One folder for each feature
    │   ├── Add[EntityName]/
    │   │   ├── Add[EntityName].cs      # Command and handler
    │   │   └── Add[EntityName].Http.cs # HTTP endpoint
    │   ├── Update[EntityName]/
    │   ├── Delete[EntityName]/
    │   ├── Get[EntityName]/
    │   └── Get[EntityName]List/
    ├── Mappings/
    │   └── [EntityName]Mapper.cs
    └── Models/
        ├── [EntityName]ForCreation.cs
        └── [EntityName]ForUpdate.cs

Databases/
└── EntityConfigurations/
    └── [EntityName]Configuration.cs    # EF Core entity configuration
```

Each feature folder has `[Feature].cs`. Each adapter file is optional: `[Feature].Http.cs`, `[Feature].Grpc.cs`, and `[Feature].Mcp.cs`. Before you add an adapter, ask the user which transports the feature needs. Add only those files. A feature can have no adapter. See `docs/backend/endpoints.md#choose-the-entrypoints-of-a-feature`.

Each entity needs `public const string IdPrefix` with 1 to 17 lower-case ASCII letters. The prefix must be unique. Each constructor must call `base(IdPrefix)` with the IdPrefix of its own type. An entity must derive from `BaseEntity`, or from an abstract base class that has no IdPrefix, never from another entity. The build fails when an entity breaks one of these rules (FSTID001-005).

An abstract base entity has no IdPrefix. Its constructor must take the prefix as a parameter and give that parameter to `BaseEntity`, for example `protected AuditedEntity(string idPrefix) : base(idPrefix) { }`. The prefix parameter must have the name `idPrefix`. Each concrete entity then calls `base(IdPrefix)` with its own IdPrefix. The build fails when the abstract constructor gives `BaseEntity` a different value, or when the abstract class has no constructor (FSTID004). It also fails when an entity derives from an abstract class that has an IdPrefix (FSTID005).

## Scenario 2: New Property on Existing Entity

When adding properties to an existing entity, update these files:

| File | Changes |
|------|---------|
| `[EntityName].cs` | Add property with private setter |
| `[EntityName]ForCreation.cs` | Add property if set at creation |
| `[EntityName]ForUpdate.cs` | Add property if updatable |
| `[EntityName]ForCreationDto.cs` | Add property if set at creation |
| `[EntityName]ForUpdateDto.cs` | Add property if updatable |
| `[EntityName]Dto.cs` | Add property for response |
| `[EntityName]Mapper.cs` | Add custom mapping if value object |
| `[EntityName]Configuration.cs` | Configure property constraints, value objects |

## Scenario 3: New Feature on Existing Entity

When adding a business operation to an existing entity:

| Component | Files to Create/Modify |
|-----------|----------------------|
| **Domain method** | `[EntityName].cs` - Add behavior method |
| **Domain event** | `DomainEvents/[EntityName][ActionPastTense].cs` - New event, with a name in the past tense |
| **Feature** | `Features/[Action][EntityName]/[Action][EntityName].cs` - New command/query |
| **Endpoint** | `Features/[Action][EntityName]/[Action][EntityName].Http.cs` - New `IEndpoint` |
| **DTOs** (if needed) | `Dtos/[Action][EntityName]Dto.cs` - Request/response |

---

## Usage Examples

### New Entity
```
Create a backend feature for managing Products with name, price, and category
```

### New Property on Existing Entity
```
Add an email property to the Customer entity
```
```
Add a status property with Draft, Active, and Archived states to the Product entity
```

### New Feature on Existing Entity
```
Add a Submit feature to the Order entity that changes status and validates requirements
```
```
Add a GetOrdersByCustomer query that returns orders for a specific customer
```

---

## Process: New Entity

1. **Gather Requirements**
   - Entity name (PascalCase, singular: `Customer`, `Order`, `Product`)
   - Properties and their types
   - Any special business rules or value objects needed

2. **Generate Domain Entity**
   - Create rich domain entity following DDD principles
   - Private setters, factory methods, encapsulated behavior
   - Value objects for complex concepts
   - See: `docs/backend/domain-entities.md`

3. **Generate Domain Events**
   - Created and Updated events (no deleted event, because deletes are soft)
   - Additional business-specific events as needed
   - Each event carries IDs and values, never an entity
   - A handler, only when a feature needs one: `IDomainEventHandler<TEvent>`, which runs in the save transaction
   - See: `docs/backend/domain-events.md`

4. **Generate DTOs and Models**
   - Read DTO for responses
   - Creation/Update DTOs for requests
   - Parameters DTO for list queries
   - Internal models for domain method contracts
   - See: `docs/backend/dtos-and-mappings.md`

5. **Generate Mapperly Mapper**
   - Entity to DTO mappings
   - DTO to internal model mappings
   - IQueryable projection support
   - See: `docs/backend/dtos-and-mappings.md`

6. **Generate CQRS Features**
   - Add, Update, Delete commands
   - Get single and Get list queries
   - MediatR handlers with proper patterns
   - See: `docs/backend/features-and-cqrs.md`

7. **Generate Endpoints**
   - Ask the user which transports each feature needs: HTTP, gRPC, MCP, or none
   - For HTTP: one `IEndpoint` in the feature folder, which sends the MediatR request
   - For HTTP: `.MapToApiVersion`, `.WithName`, `.WithTags`, and the error statuses
   - For HTTP: an XML `<summary>` on the public static `Handle` method for OpenAPI
   - For gRPC and MCP: one file each, only for the features that need them
   - For MCP: exactly one `[ToolAccess]` on the tool, with the permission that the handler requires (a `Permission.Names` constant), `ToolAccessKind.Read`, or `ToolAccessKind.ManageAllTenants`
   - See: `docs/backend/endpoints.md`

8. **Generate Entity Configuration**
   - Create `IEntityTypeConfiguration<T>` class
   - Configure property constraints and indexes
   - Configure value object mappings with `ComplexProperty`
   - See: `docs/backend/entity-configurations.md`

9. **Update Infrastructure**
   - Add DbSet to AppDbContext
   - Ensure `ApplyConfigurationsFromAssembly` is called in OnModelCreating

10. **Generate the API Contract**
   - Build the Server, then run `pnpm generate:api` in `frontend/`
   - Commit the generated files with the change. Do not edit them by hand

---

## Process: New Property

1. **Read Existing Entity**
   - Understand current entity structure and patterns
   - Identify if property is a simple type or value object

2. **Add Property to Domain Entity**
   ```csharp
   // Simple property. Mark each property for the audit log, except a flag, an enum, a Guid, an integer, or a foreign key.
   [Sensitive] public string PhoneNumber { get; private set; }

   // Value object property
   [Sensitive] public EmailAddress Email { get; private set; } = default!;
   ```

3. **Update Factory Method (if set at creation)**
   ```csharp
   public static Customer Create(CustomerForCreation forCreation)
   {
       var entity = new Customer
       {
           // ... existing properties
           PhoneNumber = forCreation.PhoneNumber,  // Add new property
       };
       // ...
   }
   ```

4. **Update Update Method (if updatable)**
   ```csharp
   public Customer Update(CustomerForUpdate forUpdate)
   {
       // ... existing updates
       PhoneNumber = forUpdate.PhoneNumber;  // Add new property
       // ...
   }
   ```

5. **Update Internal Models**
   - Add to `[EntityName]ForCreation.cs` if set at creation
   - Add to `[EntityName]ForUpdate.cs` if updatable

6. **Update DTOs**
   - Add to `[EntityName]Dto.cs` for responses
   - Add to `[EntityName]ForCreationDto.cs` if set at creation
   - Add to `[EntityName]ForUpdateDto.cs` if updatable

7. **Update Mapper (for value objects)**
   ```csharp
   // Add custom mapping for value objects
   private static string? MapEmail(EmailAddress? email) => email?.Value;
   ```

8. **Update Entity Configuration**
   ```csharp
   // Simple property
   builder.Property(e => e.PhoneNumber).HasMaxLength(20);

   // Value object
   builder.ComplexProperty(e => e.Email, email =>
   {
       email.Property(e => e.Value).HasColumnName("email").HasMaxLength(320);
   });
   ```

9. **Create Migration**
   ```bash
   dotnet ef migrations add Add[PropertyName]To[EntityName]
   ```

10. **Generate the API Contract**
   - Build the Server, then run `pnpm generate:api` in `frontend/`
   - Commit the generated files with the change. Do not edit them by hand

---

## Process: New Feature

1. **Read Existing Entity**
   - Understand current entity structure
   - Identify what state changes are needed
   - Determine guard conditions

2. **Add Domain Method to Entity**
   ```csharp
   public Order Submit()
   {
       GuardIfNotDraft("Cannot submit");

       Status = OrderStatus.Submitted();

       QueueDomainEvent(new OrderSubmitted(Id, DateTimeOffset.UtcNow));

       return this;
   }
   ```

3. **Create Domain Event (if needed)**
   ```csharp
   // DomainEvents/OrderSubmitted.cs
   public sealed record OrderSubmitted(string OrderId, DateTimeOffset SubmittedAt) : DomainEvent;
   ```

4. **Create Feature (Command or Query)**
   ```csharp
   // Features/SubmitOrder/SubmitOrder.cs
   public static class SubmitOrder
   {
       public sealed record Command(string Id) : IRequest<OrderDto>;

       public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
           : IRequestHandler<Command, OrderDto>
       {
           private readonly ILogger _logger = Log.ForContext<Handler>();

           public async Task<OrderDto> Handle(Command request, CancellationToken ct)
           {
               Activity.Current?.SetTag("app.order_id", request.Id);

               await permissionGuard.RequireAsync(Permission.ManageOrders(), ct);

               var order = await dbContext.Orders
                   .TagWithOperationCallSite("orders.submit.load")
                   .GetById(request.Id, ct);

               order.Submit();

               await dbContext.SaveChangesAsync(ct);

               _logger.Here().Information("Submitted order {OrderId}", order.Id);

               return order.ToOrderDto();
           }
       }
   }
   ```

5. **Add the HTTP Endpoint**
   ```csharp
   // Features/SubmitOrder/SubmitOrder.Http.cs
   public sealed class SubmitOrderEndpoint : IEndpoint
   {
       public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
           .MapPost("/orders/{id}/submit", Handle)
           .WithName("SubmitOrder")
           .WithTags("Orders")
           .MapToApiVersion(ApiVersions.V1)
           .RequireAuthorization()
           .ProducesProblem(StatusCodes.Status403Forbidden)
           .ProducesProblem(StatusCodes.Status404NotFound)
           .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

       /// <summary>
       /// Submits an order for processing.
       /// </summary>
       public static async Task<Ok<OrderDto>> Handle(string id, IMediator mediator, CancellationToken cancellationToken)
       {
           var result = await mediator.Send(new SubmitOrder.Command(id), cancellationToken);
           return TypedResults.Ok(result);
       }
   }
   ```

6. **Create Additional DTOs (if needed)**
   - Request DTO if the action needs input parameters
   - Custom response DTO if different from standard entity DTO

7. **Generate the API Contract**
   - Build the Server, then run `pnpm generate:api` in `frontend/`
   - Commit the generated files with the change. Do not edit them by hand

---

## Important Guidelines

### Domain Entity Design
- Use private setters for all properties
- Create static factory method `Create()` for entity creation
- Use `Update()` method for modifications. Its first line is `GuardVersion(forUpdate.Version)`, so that a stale edit form gives 409
- Implement guard methods for business rules
- Use value objects for complex domain concepts
- Queue domain events on state changes

### Property Naming
- Entity properties: PascalCase (`FirstName`, `Email`)
- DTO properties: PascalCase with `{ get; init; }`
- Route parameters: camelCase in URLs, PascalCase in code

### Value Objects to Consider
- `EmailAddress` - validated email
- `PhoneNumber` - formatted phone
- `Money` - currency with amount
- `Address` - street, city, state, zip
- Status enums with SmartEnum pattern

### Common Patterns

**Status with Behavior:**
```csharp
public class OrderStatus : ValueObject, IFixedNames
{
    private OrderStatusEnum _status;

    public bool IsFinalState() => _status.IsFinalState();
    public bool CanBeCancelled() => !IsFinalState();

    public override string ToString() => Value;

    public static List<string> ListNames() => OrderStatusEnum.List.Select(x => x.Name).ToList();

    public static OrderStatus Draft() => new("Draft");
    public static OrderStatus Submitted() => new("Submitted");
}
```

**Encapsulated Collections:**
```csharp
private readonly List<LineItem> _lineItems = [];
public IReadOnlyCollection<LineItem> LineItems => _lineItems.AsReadOnly();

public LineItem AddLineItem(Product product, int quantity)
{
    var item = LineItem.Create(product, quantity);
    _lineItems.Add(item);
    return item;
}
```

## Checklists

### New Entity Checklist
- [ ] Review generated entity for business logic completeness
- [ ] Add DbSet to `AppDbContext.cs`
- [ ] The entity is a root with `ITenantable`, or a child with one `Cascade` foreign key to its owner (see `docs/backend/audit-log.md`)
- [ ] Each property that is not a flag, an enum, a `Guid`, an integer, or a foreign key has `[Sensitive]` or `[NotSensitive]`
- [ ] Verify entity configuration is in `Databases/EntityConfigurations/` and properly applied
- [ ] Each foreign-key column to an entity ID has `HasMaxLength(EntityId.MaxLength)` and `UseCollation(EntityId.Collation)`
- [ ] Each foreign key to an entity ID has a prefix check with `EntityId.IsValid` in the entity validation
- [ ] Each text value that a request sets has a max length constant that the entity validation checks and `HasMaxLength` uses, and the entity validation refuses a character that Postgres cannot store with `ThrowWhenHasCharacterPostgresCannotStore` (see `docs/backend/domain-entities.md`)
- [ ] Add the entity to the entity graph with the `test-entity-graph` skill. Then list each text column with a max length in `StringColumnLengthTests`: a column that a request sets in `CallerColumns` with its domain write, a column with fixed options in `FixedOptionColumns`, or a column that only the server writes in `ServerWrittenColumns`
- [ ] Install MediatR if not already present
- [ ] Install Mapperly if not already present
- [ ] Run `dotnet build` to verify compilation
- [ ] Put validation in the entity, and put database checks such as unique values in the handler
- [ ] Add the permission of the commands to `Permission.cs` (a `Permission.Names` constant, a `PermissionEnum` entry, and a factory method), with a migration that gives it to the admins that exist
- [ ] Add the name of the new permission to the two lists in `PermissionTests` (`list_names_returns_all_permission_names` and `get_all_returns_all_permissions`)
- [ ] Consider what additional business methods the entity needs
- [ ] Run `dotnet ef migrations add Add[EntityName]` to create migration
- [ ] Run `pnpm generate:api` in `frontend/` and commit the generated contract

### New Property Checklist
- [ ] Property added with private setter in entity
- [ ] A property that is not a flag, an enum, a `Guid`, an integer, or a foreign key has `[Sensitive]` or `[NotSensitive]` (see `docs/backend/audit-log.md`)
- [ ] Factory method updated (if set at creation)
- [ ] Update method updated (if updatable)
- [ ] Internal models updated (ForCreation/ForUpdate)
- [ ] DTOs updated (Dto, ForCreationDto, ForUpdateDto as needed). The Dto has `Version`, and the ForUpdateDto has `required uint Version`
- [ ] Mapper updated (custom mapping for value objects)
- [ ] Entity configuration updated (constraints, value object mapping)
- [ ] A new foreign-key column to an entity ID has `HasMaxLength(EntityId.MaxLength)` and `UseCollation(EntityId.Collation)`
- [ ] A new foreign key to an entity ID has a prefix check with `EntityId.IsValid` in the entity validation
- [ ] A new text value that a request sets has a max length constant that the entity validation checks and `HasMaxLength` uses, and the entity validation refuses a character that Postgres cannot store with `ThrowWhenHasCharacterPostgresCannotStore` (see `docs/backend/domain-entities.md`)
- [ ] A new text column with a max length is in `CallerColumns`, `FixedOptionColumns`, or `ServerWrittenColumns` of `StringColumnLengthTests`. For a caller column, add the property to the input and the blueprint of the entity graph, so that the domain write can set it
- [ ] Run `dotnet build` to verify compilation
- [ ] Run `dotnet ef migrations add Add[PropertyName]To[EntityName]`
- [ ] Run `pnpm generate:api` in `frontend/` and commit the generated contract

### New Feature Checklist
- [ ] Domain method added to entity with proper guards
- [ ] Domain event created (if state change is significant)
- [ ] The handler writes through `SaveChangesAsync` or `BulkInsertAsync`. A fact that changes no data is an audit action that the handler records and saves (see `docs/backend/audit-log.md`)
- [ ] Feature folder created with the Command/Query and Handler file
- [ ] Each command calls `IPermissionGuard` as its first statement, with a permission from `Permission.cs` (a new permission has a migration that gives it to the admins that exist, and its name is in the two lists of `PermissionTests`)
- [ ] The transports of the feature are known (HTTP, gRPC, MCP, or none)
- [ ] One adapter file added for each of these transports, and no other
- [ ] If HTTP: `[Feature].Http.cs` endpoint added with its name, tag, version, and error statuses
- [ ] If MCP: `[Feature].Mcp.cs` tool added with exactly one `[ToolAccess]` that matches the check of the handler
- [ ] Additional DTOs created if needed
- [ ] Telemetry added with the `feature-telemetry` skill: query tags, id tags on the feature span, a log for each state change, and metrics if support needs a rate
- [ ] Run `dotnet build` to verify compilation
- [ ] Run `pnpm generate:api` in `frontend/` and commit the generated contract

## Reference Files

These docs give the detailed patterns:
- `docs/backend/domain-entities.md` - Rich domain entities
- `docs/backend/features-and-cqrs.md` - CQRS with MediatR
- `docs/backend/dtos-and-mappings.md` - DTOs and Mapperly
- `docs/backend/endpoints.md` - HTTP endpoints, gRPC, and MCP
- `docs/backend/domain-events.md` - Domain events
- `docs/backend/entity-configurations.md` - EF Core entity configurations
- `docs/backend/feature-telemetry.md` - Spans, logs, and metrics for production support
