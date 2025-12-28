# DTOs and Mapperly Mappings

Guidelines for creating Data Transfer Objects (DTOs) and using Mapperly for efficient object mapping.

## Directory Structure

DTOs and mappings are organized within each domain entity folder:

```
Domain/
└── [EntityName]/
    ├── Dtos/
    │   ├── [EntityName]Dto.cs                # Read DTO (responses)
    │   ├── [EntityName]ForCreationDto.cs     # Create DTO (request body)
    │   ├── [EntityName]ForUpdateDto.cs       # Update DTO (request body)
    │   └── [EntityName]ParametersDto.cs      # Query parameters DTO
    ├── Mappings/
    │   └── [EntityName]Mapper.cs             # Mapperly mapper
    └── Models/
        ├── [EntityName]ForCreation.cs        # Internal creation model
        └── [EntityName]ForUpdate.cs          # Internal update model
```

## DTO Types

### 1. Read DTO (Response)

Used for API responses. Represents how the entity appears externally.

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Dtos;

public sealed record [EntityName]Dto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Status { get; init; } = default!;
    public DateTimeOffset CreatedOn { get; init; }
    public DateTimeOffset? LastModifiedOn { get; init; }
}
```

### 2. Creation DTO (Request Body)

Used for POST requests. Contains only fields needed for creation.

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Dtos;

public sealed record [EntityName]ForCreationDto
{
    public string Name { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string? Description { get; init; }
}
```

### 3. Update DTO (Request Body)

Used for PUT/PATCH requests. Contains only fields that can be updated.

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Dtos;

public sealed record [EntityName]ForUpdateDto
{
    public string Name { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string? Description { get; init; }
}
```

### 4. Parameters DTO (Query String)

Used for filtering, sorting, and pagination on list endpoints.

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Dtos;

public sealed class [EntityName]ParametersDto : BasePaginationParameters
{
    public string? Filters { get; set; }
    public string? SortOrder { get; set; }
}
```

## Internal Models

Models are internal contracts used by domain entity methods. They bridge DTOs to domain operations.

### Creation Model

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Models;

public sealed record [EntityName]ForCreation
{
    public string Name { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string? Description { get; init; }
}
```

### Update Model

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Models;

public sealed record [EntityName]ForUpdate
{
    public string Name { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string? Description { get; init; }
}
```

## Mapperly Mapper

Use Mapperly for compile-time source generation of mapping code.

### Basic Mapper Structure

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Mappings;

[Mapper]
public static partial class [EntityName]Mapper
{
    // Entity to DTO
    public static partial [EntityName]Dto To[EntityName]Dto(this [EntityName] entity);

    // For queryable projection (efficient database queries)
    public static partial IQueryable<[EntityName]Dto> To[EntityName]DtoQueryable(
        this IQueryable<[EntityName]> queryable);

    // DTO to internal model
    public static partial [EntityName]ForCreation To[EntityName]ForCreation(
        this [EntityName]ForCreationDto dto);

    public static partial [EntityName]ForUpdate To[EntityName]ForUpdate(
        this [EntityName]ForUpdateDto dto);
}
```

### Mapping Value Objects

When mapping value objects that expose a `Value` property:

```csharp
[Mapper]
public static partial class [EntityName]Mapper
{
    public static partial [EntityName]Dto To[EntityName]Dto(this [EntityName] entity);

    // Custom mapping for value objects
    private static string MapStatus([EntityName]Status status) => status.Value;
    private static string? MapEmail(EmailAddress? email) => email?.Value;
}
```

### Mapping Collections

```csharp
[Mapper]
public static partial class OrderMapper
{
    public static partial OrderDto ToOrderDto(this Order entity);

    // Collection mapping happens automatically
    // Order.LineItems -> OrderDto.LineItems
    public static partial LineItemDto ToLineItemDto(this LineItem entity);
}
```

### Conditional Mapping

For nullable or conditional mappings:

```csharp
[Mapper]
public static partial class [EntityName]Mapper
{
    [MapperIgnoreSource(nameof([EntityName].InternalField))]
    public static partial [EntityName]Dto To[EntityName]Dto(this [EntityName] entity);

    [MapProperty(nameof([EntityName].FullName), nameof([EntityName]Dto.DisplayName))]
    public static partial [EntityName]SummaryDto To[EntityName]SummaryDto(this [EntityName] entity);
}
```

## Key Principles

### 1. Use Sealed Records for DTOs

Records provide immutability and value-based equality:

```csharp
public sealed record CustomerDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
}
```

### 2. Use `init` Accessors

DTOs should be immutable after construction:

```csharp
// Good
public string Name { get; init; } = default!;

// Avoid
public string Name { get; set; }
```

### 3. Use `= default!` for Required Properties

Suppress nullability warnings for properties that will always be set:

```csharp
public string Name { get; init; } = default!;  // Required, always set
public string? Description { get; init; }       // Optional, nullable
```

### 4. Keep DTOs Flat

Prefer flat structures over nested objects for API responses:

```csharp
// Preferred - flat structure
public sealed record OrderDto
{
    public Guid Id { get; init; }
    public string CustomerName { get; init; } = default!;
    public string CustomerEmail { get; init; } = default!;
}

// Use nested only when semantically appropriate
public sealed record OrderDto
{
    public Guid Id { get; init; }
    public CustomerSummaryDto Customer { get; init; } = default!;
    public IReadOnlyList<LineItemDto> LineItems { get; init; } = [];
}
```

### 5. Use IQueryable Projections for List Queries

Mapperly generates efficient SQL projections:

```csharp
// In mapper
public static partial IQueryable<CustomerDto> ToCustomerDtoQueryable(
    this IQueryable<Customer> queryable);

// In handler - efficient single query
var dtos = dbContext.Customers
    .AsNoTracking()
    .ToCustomerDtoQueryable();
```

### 6. Extension Methods for Mapping

Define mappers as extension methods for fluent usage:

```csharp
// Usage
var dto = entity.ToCustomerDto();
var model = dto.ToCustomerForCreation();
```

### 7. Separate Read and Write DTOs

Never use the same DTO for reading and writing:

```csharp
// Read (response) - includes Id and audit fields
public sealed record CustomerDto { ... }

// Write (request) - no Id, no audit fields
public sealed record CustomerForCreationDto { ... }
public sealed record CustomerForUpdateDto { ... }
```

## DTO vs Model Distinction

| Aspect | DTO | Model |
|--------|-----|-------|
| **Purpose** | API contract (external) | Domain method parameter (internal) |
| **Location** | `Dtos/` folder | `Models/` folder |
| **Used by** | Controllers, API clients | Domain entity methods |
| **Validation** | FluentValidation in Features | Domain entity validation |
| **Example** | `CustomerForCreationDto` | `CustomerForCreation` |

### Flow Example

```
Controller receives CustomerForCreationDto
    ↓ (Mapperly)
Feature converts to CustomerForCreation (model)
    ↓ (passed to)
Customer.Create(CustomerForCreation model)
    ↓ (domain logic, validation)
Customer entity created
    ↓ (Mapperly)
CustomerDto returned to controller
```

## Anti-Patterns to Avoid

### Don't Expose Domain Entities

```csharp
// Bad - exposing domain entity
[HttpGet("{id}")]
public async Task<Customer> GetCustomer(Guid id) { ... }

// Good - return DTO
[HttpGet("{id}")]
public async Task<CustomerDto> GetCustomer(Guid id) { ... }
```

### Don't Mix Read/Write DTOs

```csharp
// Bad - using same DTO for create and response
public sealed record CustomerDto { ... }

// Good - separate DTOs
public sealed record CustomerDto { ... }           // Read
public sealed record CustomerForCreationDto { ... } // Write
```

### Don't Put Business Logic in Mappers

```csharp
// Bad - business logic in mapper
private static string MapStatus(Order order) =>
    order.Items.Count > 10 ? "Large" : "Small";

// Good - domain entity exposes computed property
public class Order
{
    public string SizeCategory => Items.Count > 10 ? "Large" : "Small";
}
```

### Don't Include Sensitive Data in DTOs

```csharp
// Bad - exposing internal data
public sealed record UserDto
{
    public string PasswordHash { get; init; }  // Never!
    public string InternalNotes { get; init; } // Internal only
}

// Good - only public data
public sealed record UserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; }
    public string DisplayName { get; init; }
}
```
