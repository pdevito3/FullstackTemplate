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
   - If the list has a search box, build the pattern with `LikePattern` and pass `LikePattern.EscapeCharacter`. See `docs/backend/features-and-cqrs.md#search-text`.
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
   - Regenerate the API contract with the `api-contract` skill
   - Commit the generated files with the change. Do not edit them by hand

---

## Process: New Property

1. **Read Existing Entity**
   - Understand current entity structure and patterns
   - Identify if property is a simple type or value object

2. **Add Property to Domain Entity**
   ```csharp
   // A text value gets a max length constant. A value object, such as EmailAddress, has its own.
   public const int PhoneNumberMaxLength = 20;

   // Simple property. Mark each property for the audit log, except a flag, an enum, a Guid, an integer, or a foreign key.
   // A value that the caller can leave empty is nullable. A required one is a string with = default!.
   [Sensitive] public string? PhoneNumber { get; private set; }

   // Value object property
   [Sensitive] public EmailAddress Email { get; private set; } = default!;
   ```
   - Check the text value in `Validate[EntityName]` (rule 10 of `docs/backend/domain-entities.md`). If the file has no `using FullstackTemplate.Server.Exceptions;`, write `Exceptions.ValidationException`. Check a required value with `ThrowWhenNullOrWhitespace` first, and do not put it in an `is not null` block
   ```csharp
   if (entity.PhoneNumber is not null)
   {
       ValidationException.ThrowWhenLongerThan(entity.PhoneNumber, PhoneNumberMaxLength, "phoneNumber",
           $"A phone number can have no more than {PhoneNumberMaxLength} characters.");
       ValidationException.ThrowWhenHasCharacterPostgresCannotStore(entity.PhoneNumber, "phoneNumber",
           "A phone number has a character that is not allowed.");
   }
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
       ValidateCustomer(entity);
       // ...
   }
   ```

4. **Update Update Method (if updatable)**
   ```csharp
   public Customer Update(CustomerForUpdate forUpdate)
   {
       GuardVersion(forUpdate.Version);
       // ... existing updates
       PhoneNumber = forUpdate.PhoneNumber;  // Add new property

       ValidateCustomer(this);
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
   builder.Property(e => e.PhoneNumber).HasMaxLength(Customer.PhoneNumberMaxLength);

   // Value object
   builder.ComplexProperty(e => e.Email, email =>
   {
       email.Property(e => e.Value).HasColumnName("email").HasMaxLength(EmailAddress.MaxLength).IsRequired();
   });
   ```

9. **Update the MCP and gRPC Adapters**
   - An MCP tool or a gRPC method that makes a `[EntityName]ForCreationDto` or a `[EntityName]ForUpdateDto` sets each value by name. An update sets each value, so a value that the adapter does not send becomes null
   - Add a parameter for the property to each of these adapters and to each SPA call of the update, and send it in the DTO. For gRPC, also add the field to the messages in the `.proto` file and to the mapping of the response
   - Follow "MCP versions" and the gRPC rules of `docs/backend/endpoints.md`: a new required parameter of an MCP tool that exists needs a new tool name (an optional parameter with the default `null`, after `CancellationToken`, keeps the name), and a gRPC field that can have no value is `optional` (see "Adding a New Property" in `templates.md`)

10. **Update the Test Lists**
   - Add the property to the input and the blueprint of the entity graph with the `test-entity-graph` skill
   - Give a required value a `Derive` in the blueprint. If the entity has `[EntityName]Rules` and shape fakers, put a rule for each new text property, required or nullable, there. Use it in each shape faker and in the `Derive`. Otherwise an `AutoFaker` fills a nullable text with a value that is too long, and unrelated tests fail at random
   - List each new text column, also the column of a value object, in `CallerColumns` of `StringColumnLengthTests` with its domain write (see "Adding a New Property" in `templates.md`)

11. **Create Migration**
   ```bash
   dotnet ef migrations add Add[PropertyName]To[EntityName]
   ```

12. **Generate the API Contract**
   - Regenerate the API contract with the `api-contract` skill
   - Commit the generated files with the change. Do not edit them by hand

---

## Process: New Feature

1. **Read Existing Entity**
   - Understand current entity structure
   - Identify what state changes are needed
   - Determine guard conditions

2. **Choose Where the Rule Lives**
   - Use the decision shape of `docs/backend/features-and-cqrs.md#one-rule-set-for-two-callers` in two cases. The first is when a second caller needs the same rules, such as a list of what is possible and the command that does it. The second is when a rule reads more than one aggregate
   - Write the case record, the rule list, and the refusal type in `Domain/[Entity]s/`. Keep the command handler to one load, one `Decide`, and one save
   - Otherwise, put the rule in a guard of the entity, as in the next step
   - If the SPA shows a button for the action, add the action to the action value object of the entity, such as `UserAction`, so that it is in `AllowedActions`. The command calls `GuardAllows` first after the load. See `docs/backend/domain-entities.md#allowed-actions`

3. **Add Domain Method to Entity**
   ```csharp
   public Order Submit()
   {
       GuardIfNotDraft("Cannot submit");

       Status = OrderStatus.Submitted();

       QueueDomainEvent(new OrderSubmitted(Id, DateTimeOffset.UtcNow));

       return this;
   }
   ```

4. **Create Domain Event (if needed)**
   ```csharp
   // DomainEvents/OrderSubmitted.cs
   public sealed record OrderSubmitted(string OrderId, DateTimeOffset SubmittedAt) : DomainEvent;
   ```

5. **Pick the Concurrency Tool**
   - Pick the concurrency tool of the command from `docs/backend/concurrency.md`: a version, a run again, or a lock
   - A version: the command applies fields that the user saw and edited. The DTO has `required uint Version`, and the entity method calls `GuardVersion`
   - A run again: the command sends no version, and the server decides it from the rows alone. The command carries `IRunsAgainWhenRaced`. Send it only from an entrypoint, save one time, and do the logs and the metrics after the save. A second run after its own commit must give the same end state, so do not mark a command that adds to a value, such as an increment
   - A lock: the rule reads rows that the command does not write, such as a count of open bookings. Write the lock in the XML comment of the command
   - A create of a new aggregate root needs no version and no run again. It needs a lock only when its rule reads other rows

6. **Create Feature (Command or Query)**
   ```csharp
   // Features/SubmitOrder/SubmitOrder.cs
   public static class SubmitOrder
   {
       public sealed record Command(string Id) : IRequest<OrderDto>, IRunsAgainWhenRaced;

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

7. **Add the HTTP Endpoint**
   ```csharp
   // Features/SubmitOrder/SubmitOrder.Http.cs
   namespace FullstackTemplate.Server.Domain.Orders.Features;

   using Dtos;
   using MediatR;
   using Microsoft.AspNetCore.Http.HttpResults;
   using Resources.Entrypoints.Http;

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
           .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
           .ProducesValidationProblem(StatusCodes.Status409Conflict);

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

8. **Create Additional DTOs (if needed)**
   - Request DTO if the action needs input parameters
   - Custom response DTO if different from standard entity DTO

9. **Generate the API Contract**
   - Regenerate the API contract with the `api-contract` skill
   - Commit the generated files with the change. Do not edit them by hand

---

## Important Guidelines

### Domain Entity Design
- Use private setters for all properties
- Create static factory method `Create()` for entity creation
- Make the ID of a new entity before the save, as `BaseEntity` does. Do not use a key that the database makes. The audit entries of the save need the ID, and after a lost reply of the commit the save finds them, so it does not add a second row
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

**One Rule Set for Two Callers:** (see `docs/backend/features-and-cqrs.md#one-rule-set-for-two-callers`)
- A case record holds each fact that the rules read. One loader fills it for both callers
- One static class holds the ordered rules. Each rule returns a refusal or null, and `Decide` returns the first refusal
- The refusal is a closed set of sealed records. The handler maps each kind to an exception (409 or 422)
- `Decide` alone makes the proof type, and the write method takes the proof
- The list keeps each candidate that `Decide` offers. The command calls `Decide` one time and throws the refusal

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
- [ ] Each text value that a request sets has a max length constant that the entity validation checks, `HasMaxLength` uses, and `[SchemaMaxLength]` puts on the request DTO, and the entity validation refuses a character that Postgres cannot store with `ThrowWhenHasCharacterPostgresCannotStore` (see `docs/backend/domain-entities.md`)
- [ ] Add the entity to the entity graph with the `test-entity-graph` skill. Then list each text column with a max length in `StringColumnLengthTests`: a column that a request sets in `CallerColumns` with its domain write, a column with fixed options in `FixedOptionColumns`, or a column that only the server writes in `ServerWrittenColumns`
- [ ] Install MediatR if not already present
- [ ] Install Mapperly if not already present
- [ ] Run `dotnet build` to verify compilation
- [ ] Put validation in the entity, and put database checks such as unique values in the handler
- [ ] Add the permission of the commands to `Permission.cs` (a `Permission.Names` constant, a `PermissionEnum` entry, and a factory method), with a migration that gives it to the admins that exist
- [ ] Add the name of the new permission to the two lists in `PermissionTests` (`list_names_returns_all_permission_names` and `get_all_returns_all_permissions`)
- [ ] Consider what additional business methods the entity needs
- [ ] Run `dotnet ef migrations add Add[EntityName]` to create migration
- [ ] Regenerate and commit the API contract with the `api-contract` skill

### New Property Checklist
- [ ] Property added with private setter in entity
- [ ] A property that is not a flag, an enum, a `Guid`, an integer, or a foreign key has `[Sensitive]` or `[NotSensitive]` (see `docs/backend/audit-log.md`)
- [ ] Factory method updated (if set at creation)
- [ ] Update method updated (if updatable)
- [ ] Each MCP tool, gRPC method, and SPA call that makes a `[EntityName]ForCreationDto` or a `[EntityName]ForUpdateDto` takes and sends the new property
- [ ] Internal models updated (ForCreation/ForUpdate)
- [ ] DTOs updated (Dto, ForCreationDto, ForUpdateDto as needed). The Dto has `Version`, and the ForUpdateDto has `required uint Version`
- [ ] Mapper updated (custom mapping for value objects)
- [ ] Entity configuration updated (constraints, value object mapping)
- [ ] A new foreign-key column to an entity ID has `HasMaxLength(EntityId.MaxLength)` and `UseCollation(EntityId.Collation)`
- [ ] A new foreign key to an entity ID has a prefix check with `EntityId.IsValid` in the entity validation
- [ ] A new text value that a request sets has a max length constant that the entity validation checks, `HasMaxLength` uses, and `[SchemaMaxLength]` puts on the request DTO, and the entity validation refuses a character that Postgres cannot store with `ThrowWhenHasCharacterPostgresCannotStore` (see `docs/backend/domain-entities.md`)
- [ ] A new text column with a max length is in `CallerColumns`, `FixedOptionColumns`, or `ServerWrittenColumns` of `StringColumnLengthTests`. For a caller column, add the property to the input and the blueprint of the entity graph, so that the domain write can set it
- [ ] Run `dotnet build` to verify compilation
- [ ] Run `dotnet ef migrations add Add[PropertyName]To[EntityName]`
- [ ] Regenerate and commit the API contract with the `api-contract` skill

### New Feature Checklist
- [ ] Domain method added to entity with proper guards
- [ ] If the SPA shows a button for the action: the action is in the action value object, the read DTO has `AllowedActions`, and the command calls `GuardAllows`
- [ ] Each property that the domain method sets, such as `[ActionPastTense]At`, is on the entity, the `[EntityName]Dto`, and the configuration (see "Process: New Property")
- [ ] Domain event created (if state change is significant)
- [ ] The handler writes through `SaveChangesAsync` or `BulkInsertAsync`. A fact that changes no data is an audit action that the handler records and saves (see `docs/backend/audit-log.md`)
- [ ] Feature folder created with the Command/Query and Handler file
- [ ] The command sends a version, carries `IRunsAgainWhenRaced`, or takes a lock that `docs/backend/concurrency.md` names. A create of a new aggregate root needs a lock only when its rule reads other rows
- [ ] Each command calls `IPermissionGuard` as its first statement, with a permission from `Permission.cs` (a new permission has a migration that gives it to the admins that exist, and its name is in the two lists of `PermissionTests`)
- [ ] The transports of the feature are known (HTTP, gRPC, MCP, or none)
- [ ] One adapter file added for each of these transports, and no other
- [ ] If HTTP: `[Feature].Http.cs` endpoint added with its name, tag, version, and error statuses
- [ ] If MCP: `[Feature].Mcp.cs` tool added with exactly one `[ToolAccess]` that matches the check of the handler. Add the tool name to the expected lists of `ToolsOfEachCaller` in `TenantMcpToolTests` for each caller that can use it
- [ ] Additional DTOs created if needed
- [ ] Telemetry added with the `feature-telemetry` skill: query tags, id tags on the feature span, a log for each state change, and metrics if support needs a rate
- [ ] A command that runs again or takes a lock has a race test (see the `writing-tests` skill)
- [ ] Run `dotnet build` to verify compilation
- [ ] Regenerate and commit the API contract with the `api-contract` skill

## Reference Files

These docs give the detailed patterns:
- `docs/backend/domain-entities.md` - Rich domain entities
- `docs/backend/features-and-cqrs.md` - CQRS with MediatR
- `docs/backend/dtos-and-mappings.md` - DTOs and Mapperly
- `docs/backend/endpoints.md` - HTTP endpoints, gRPC, and MCP
- `docs/backend/domain-events.md` - Domain events
- `docs/backend/entity-configurations.md` - EF Core entity configurations
- `docs/backend/feature-telemetry.md` - Spans, logs, and metrics for production support
- `docs/backend/source-generators.md` - Generators before reflection. For a mapper or a regex, use the `source-generators` skill
