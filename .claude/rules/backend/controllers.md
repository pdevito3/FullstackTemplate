# Controllers

Guidelines for creating thin API controllers that delegate to MediatR handlers.

## Directory Structure

Controllers are organized within each domain entity folder by API version:

```
Domain/
└── [EntityName]/
    └── Controllers/
        ├── v1/
        │   └── [EntityName]sController.cs
        └── v2/
            └── [EntityName]sController.cs    # New version when breaking changes needed
```

## Controller Structure

### Basic Controller Template

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Controllers.v1;

[ApiController]
[Route("api/v{v:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public sealed class [EntityName]sController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Gets a single [EntityName] by ID.
    /// </summary>
    [Authorize]
    [HttpGet("{id:guid}", Name = "Get[EntityName]")]
    [ProducesResponseType(typeof([EntityName]Dto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<[EntityName]Dto>> Get[EntityName](Guid id)
    {
        var query = new Get[EntityName].Query(id);
        var result = await mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Gets a paginated list of [EntityName]s.
    /// </summary>
    [Authorize]
    [HttpGet(Name = "Get[EntityName]List")]
    [ProducesResponseType(typeof(PagedList<[EntityName]Dto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedList<[EntityName]Dto>>> Get[EntityName]List(
        [FromQuery] [EntityName]ParametersDto parameters)
    {
        var query = new Get[EntityName]List.Query(parameters);
        var result = await mediator.Send(query);

        Response.AddPaginationHeader(result);

        return Ok(result);
    }

    /// <summary>
    /// Creates a new [EntityName].
    /// </summary>
    [Authorize]
    [HttpPost(Name = "Add[EntityName]")]
    [ProducesResponseType(typeof([EntityName]Dto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<[EntityName]Dto>> Add[EntityName](
        [FromBody] [EntityName]ForCreationDto dto)
    {
        var command = new Add[EntityName].Command(dto);
        var result = await mediator.Send(command);

        return CreatedAtRoute("Get[EntityName]",
            new { id = result.Id },
            result);
    }

    /// <summary>
    /// Updates an existing [EntityName].
    /// </summary>
    [Authorize]
    [HttpPut("{id:guid}", Name = "Update[EntityName]")]
    [ProducesResponseType(typeof([EntityName]Dto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<[EntityName]Dto>> Update[EntityName](
        Guid id,
        [FromBody] [EntityName]ForUpdateDto dto)
    {
        var command = new Update[EntityName].Command(id, dto);
        var result = await mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Deletes a [EntityName].
    /// </summary>
    [Authorize]
    [HttpDelete("{id:guid}", Name = "Delete[EntityName]")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete[EntityName](Guid id)
    {
        var command = new Delete[EntityName].Command(id);
        await mediator.Send(command);
        return NoContent();
    }
}
```

## Key Principles

### 1. Controllers Are Thin Orchestration Layers

Controllers should only:
- Receive HTTP requests
- Create MediatR commands/queries
- Return appropriate HTTP responses

```csharp
// Good - thin controller
public async Task<ActionResult<OrderDto>> GetOrder(Guid id)
{
    var query = new GetOrder.Query(id);
    var result = await mediator.Send(query);
    return Ok(result);
}

// Bad - business logic in controller
public async Task<ActionResult<OrderDto>> GetOrder(Guid id)
{
    var order = await dbContext.Orders.FindAsync(id);
    if (order == null) return NotFound();
    if (order.Status == "Draft" && !User.IsInRole("Admin"))
        return Forbid();
    // ... more logic
}
```

### 2. Use Primary Constructor DI

```csharp
// Good - primary constructor
public sealed class OrdersController(IMediator mediator) : ControllerBase

// Avoid - field injection
public sealed class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }
}
```

### 3. Sealed Controllers

Mark controllers as sealed since they shouldn't be inherited:

```csharp
public sealed class CustomersController(IMediator mediator) : ControllerBase
```

### 4. Use Route Constraints

Add type constraints to route parameters:

```csharp
[HttpGet("{id:guid}")]           // GUID constraint
[HttpGet("{code:alpha}")]        // Alphabetic only
[HttpGet("{page:int:min(1)}")]   // Integer with minimum
```

### 5. Named Routes for CreatedAtRoute

Always name routes for use with `CreatedAtRoute`:

```csharp
[HttpGet("{id:guid}", Name = "GetCustomer")]
public async Task<ActionResult<CustomerDto>> GetCustomer(Guid id) { ... }

[HttpPost(Name = "AddCustomer")]
public async Task<ActionResult<CustomerDto>> AddCustomer(CustomerForCreationDto dto)
{
    // ...
    return CreatedAtRoute("GetCustomer", new { id = result.Id }, result);
}
```

### 6. Document with XML Comments and ProducesResponseType

```csharp
/// <summary>
/// Creates a new customer in the system.
/// </summary>
/// <param name="dto">The customer creation data.</param>
/// <returns>The created customer.</returns>
/// <response code="201">Returns the newly created customer.</response>
/// <response code="400">If the request data is invalid.</response>
[HttpPost]
[ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<ActionResult<CustomerDto>> AddCustomer(CustomerForCreationDto dto)
```

### 7. Use Appropriate Authorization

```csharp
// Require authentication for all actions
[Authorize]
[HttpGet]
public async Task<ActionResult<CustomerDto>> GetCustomer(Guid id) { ... }

// Require specific role
[Authorize(Roles = "Admin")]
[HttpDelete("{id:guid}")]
public async Task<ActionResult> DeleteCustomer(Guid id) { ... }

// Allow anonymous access
[AllowAnonymous]
[HttpGet("public")]
public async Task<ActionResult<IEnumerable<ProductDto>>> GetPublicProducts() { ... }
```

## HTTP Method Conventions

| Operation | HTTP Method | Route | Returns |
|-----------|-------------|-------|---------|
| Get single | GET | `/{id}` | 200 OK, 404 Not Found |
| Get list | GET | `/` | 200 OK |
| Create | POST | `/` | 201 Created |
| Full update | PUT | `/{id}` | 200 OK, 404 Not Found |
| Partial update | PATCH | `/{id}` | 200 OK, 404 Not Found |
| Delete | DELETE | `/{id}` | 204 No Content, 404 Not Found |
| Action | POST | `/{id}/action` | Varies |

## Pagination Headers

For list endpoints, add pagination metadata to response headers. The `PagedList<T>` class contains all pagination information:

```csharp
[HttpGet]
public async Task<ActionResult<PagedList<CustomerDto>>> GetCustomerList(
    [FromQuery] CustomerParametersDto parameters)
{
    var query = new GetCustomerList.Query(parameters);
    var result = await mediator.Send(query);

    Response.AddPaginationHeader(result);

    return Ok(result);
}
```

## Business Action Endpoints

For domain-specific operations beyond CRUD:

```csharp
/// <summary>
/// Submits an order for processing.
/// </summary>
[Authorize]
[HttpPost("{id:guid}/submit", Name = "SubmitOrder")]
[ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<ActionResult<OrderDto>> SubmitOrder(Guid id)
{
    var command = new SubmitOrder.Command(id);
    var result = await mediator.Send(command);
    return Ok(result);
}

/// <summary>
/// Cancels an order.
/// </summary>
[Authorize]
[HttpPost("{id:guid}/cancel", Name = "CancelOrder")]
[ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<ActionResult<OrderDto>> CancelOrder(
    Guid id,
    [FromBody] CancelOrderDto dto)
{
    var command = new CancelOrder.Command(id, dto.Reason);
    var result = await mediator.Send(command);
    return Ok(result);
}
```

## API Versioning

### When to Create a New Version

Create a new API version when:
- Removing or renaming properties in DTOs
- Changing property types
- Changing route structures
- Removing endpoints
- Changing authentication requirements

### Versioned Controller Structure

```csharp
// v1 - original implementation
namespace FullstackTemplate.Server.Domain.Customers.Controllers.v1;

[ApiController]
[Route("api/v{v:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public sealed class CustomersController(IMediator mediator) : ControllerBase
{
    // Original endpoints
}

// v2 - breaking changes
namespace FullstackTemplate.Server.Domain.Customers.Controllers.v2;

[ApiController]
[Route("api/v{v:apiVersion}/[controller]")]
[ApiVersion("2.0")]
public sealed class CustomersController(IMediator mediator) : ControllerBase
{
    // New implementation with breaking changes
}
```

### Register New Versions

Update `ApiVersioningExtension.cs`:

```csharp
public static ApiVersionSet GetApiVersionSet(this WebApplication app)
{
    return app.NewApiVersionSet()
        .HasApiVersion(new ApiVersion(1, 0))
        .HasApiVersion(new ApiVersion(2, 0))  // Add new version
        .ReportApiVersions()
        .Build();
}
```

## Anti-Patterns to Avoid

### Don't Inject DbContext Directly

```csharp
// Bad - direct data access
public sealed class CustomersController(AppDbContext dbContext) : ControllerBase
{
    public async Task<ActionResult<CustomerDto>> GetCustomer(Guid id)
    {
        var customer = await dbContext.Customers.FindAsync(id);
        return Ok(customer.ToDto());
    }
}

// Good - delegate to MediatR
public sealed class CustomersController(IMediator mediator) : ControllerBase
{
    public async Task<ActionResult<CustomerDto>> GetCustomer(Guid id)
    {
        var query = new GetCustomer.Query(id);
        var result = await mediator.Send(query);
        return Ok(result);
    }
}
```

### Don't Handle Exceptions in Controllers

```csharp
// Bad - manual exception handling
public async Task<ActionResult<CustomerDto>> GetCustomer(Guid id)
{
    try
    {
        var query = new GetCustomer.Query(id);
        return Ok(await mediator.Send(query));
    }
    catch (NotFoundException)
    {
        return NotFound();
    }
}

// Good - let global exception handler do it
public async Task<ActionResult<CustomerDto>> GetCustomer(Guid id)
{
    var query = new GetCustomer.Query(id);
    var result = await mediator.Send(query);
    return Ok(result);
}
```

### Don't Return Domain Entities

```csharp
// Bad - exposing domain entity
public async Task<ActionResult<Customer>> GetCustomer(Guid id) { ... }

// Good - return DTO
public async Task<ActionResult<CustomerDto>> GetCustomer(Guid id) { ... }
```
