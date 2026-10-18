# Backend Feature Templates

Complete code templates for generating a vertical slice feature. Replace `[EntityName]` with the actual entity name (e.g., `Customer`, `Order`).

## 1. Domain Entity

**File:** `Domain/[EntityName]s/[EntityName].cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s;

using FullstackTemplate.Server.Domain.[EntityName]s.DomainEvents;
using FullstackTemplate.Server.Domain.[EntityName]s.Models;
using FullstackTemplate.Server.Exceptions;

public class [EntityName] : BaseEntity
{
    // A short, unique type prefix. The ID is "[prefix]_{uuidv7}".
    public const string IdPrefix = "[prefix]";

    // Properties with private setters
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }

    // ID foreign keys
    // public string TenantId { get; private set; } = default!;

    // Value object properties
    // public EmailAddress Email { get; private set; } = default!;
    // public [EntityName]Status Status { get; private set; } = default!;

    // Navigation properties with encapsulated collections
    // private readonly List<RelatedEntity> _relatedEntities = [];
    // public IReadOnlyCollection<RelatedEntity> RelatedEntities => _relatedEntities.AsReadOnly();

    /// <summary>
    /// Creates a new [EntityName].
    /// </summary>
    public static [EntityName] Create([EntityName]ForCreation forCreation)
    {
        var entity = new [EntityName]
        {
            Name = forCreation.Name,
            Description = forCreation.Description,
        };
        // A test can give a fixed ID. Set it before you add child rows, so that they get the final ID.
        if (forCreation.Id is not null)
            entity.SetId(forCreation.Id);

        Validate[EntityName](entity);
        entity.QueueDomainEvent(new [EntityName]Created(entity));

        return entity;
    }

    /// <summary>
    /// Updates the [EntityName].
    /// </summary>
    public [EntityName] Update([EntityName]ForUpdate forUpdate)
    {
        Name = forUpdate.Name;
        Description = forUpdate.Description;

        Validate[EntityName](this);
        QueueDomainEvent(new [EntityName]Updated(Id));

        return this;
    }

    private static void Validate[EntityName]([EntityName] entity)
    {
        ValidationException.ThrowWhenNullOrWhitespace(entity.Name, "Please provide a name.");

        // Check the prefix of each ID foreign key, as User checks its TenantId.
        // ValidationException.ThrowWhenNullOrWhitespace(entity.TenantId, "Please provide a tenant.");
        // ValidationException.Must(EntityId.IsValid(entity.TenantId, Tenant.IdPrefix), "Please provide a valid tenant ID.");
    }

    // Business methods
    // public [EntityName] Submit()
    // {
    //     GuardIfInFinalState("Cannot submit");
    //     Status = [EntityName]Status.Submitted();
    //     QueueDomainEvent(new [EntityName]Submitted(Id));
    //     return this;
    // }

    // Guard methods
    // private void GuardIfInFinalState(string message)
    // {
    //     if (Status.IsFinalState())
    //         throw new ValidationException(nameof([EntityName]), message);
    // }

    // Constructor for EF Core. It gives BaseEntity the ID prefix.
    protected [EntityName]() : base(IdPrefix) { }
}
```

## 2. Domain Events

**File:** `Domain/[EntityName]s/DomainEvents/[EntityName]Created.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.DomainEvents;

public sealed record [EntityName]Created([EntityName] [EntityName]) : DomainEvent;
```

**File:** `Domain/[EntityName]s/DomainEvents/[EntityName]Updated.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.DomainEvents;

public sealed record [EntityName]Updated(string Id) : DomainEvent;
```

Do not add a deleted event. The context does soft deletes, and no handler needs the event.

## 3. DTOs

**File:** `Domain/[EntityName]s/Dtos/[EntityName]Dto.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Dtos;

public sealed record [EntityName]Dto
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public DateTimeOffset CreatedOn { get; init; }
    public string? CreatedBy { get; init; }
    public DateTimeOffset? LastModifiedOn { get; init; }
    public string? LastModifiedBy { get; init; }
}
```

**File:** `Domain/[EntityName]s/Dtos/[EntityName]ForCreationDto.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Dtos;

public sealed record [EntityName]ForCreationDto
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
}
```

**File:** `Domain/[EntityName]s/Dtos/[EntityName]ForUpdateDto.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Dtos;

public sealed record [EntityName]ForUpdateDto
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
}
```

**File:** `Domain/[EntityName]s/Dtos/[EntityName]ParametersDto.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Dtos;

public sealed class [EntityName]ParametersDto : BasePaginationParameters
{
    public string? Filters { get; set; }
    public string? SortOrder { get; set; }
}
```

## 4. Models

**File:** `Domain/[EntityName]s/Models/[EntityName]ForCreation.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Models;

public sealed record [EntityName]ForCreation
{
    /// <summary>A fixed ID with the prefix of the entity. When it is null, the entity makes a new ID.</summary>
    public string? Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
}
```

**File:** `Domain/[EntityName]s/Models/[EntityName]ForUpdate.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Models;

public sealed record [EntityName]ForUpdate
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
}
```

## 5. Mapperly Mapper

**File:** `Domain/[EntityName]s/Mappings/[EntityName]Mapper.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Mappings;

using FullstackTemplate.Server.Domain.[EntityName]s.Dtos;
using FullstackTemplate.Server.Domain.[EntityName]s.Models;
using Riok.Mapperly.Abstractions;

[Mapper]
public static partial class [EntityName]Mapper
{
    [MapperIgnoreSource(nameof([EntityName].IsDeleted))]
    [MapperIgnoreSource(nameof([EntityName].DomainEvents))]
    public static partial [EntityName]Dto To[EntityName]Dto(this [EntityName] entity);

    public static partial IQueryable<[EntityName]Dto> To[EntityName]DtoQueryable(
        this IQueryable<[EntityName]> queryable);

    // The API does not take an ID. Only tests set a fixed ID on the model.
    [MapperIgnoreTarget(nameof([EntityName]ForCreation.Id))]
    public static partial [EntityName]ForCreation To[EntityName]ForCreation(
        this [EntityName]ForCreationDto dto);

    public static partial [EntityName]ForUpdate To[EntityName]ForUpdate(
        this [EntityName]ForUpdateDto dto);

    // Add custom mappings for value objects here
    // private static string MapStatus([EntityName]Status status) => status.Value;
}
```

## 6. Features

**File:** `Domain/[EntityName]s/Features/Add[EntityName].cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using FullstackTemplate.Server.Domain.[EntityName]s.Dtos;
using FullstackTemplate.Server.Domain.[EntityName]s.Mappings;
using MediatR;

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

**File:** `Domain/[EntityName]s/Features/Update[EntityName].cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using FullstackTemplate.Server.Domain.[EntityName]s.Dtos;
using FullstackTemplate.Server.Domain.[EntityName]s.Mappings;
using MediatR;

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

**File:** `Domain/[EntityName]s/Features/Delete[EntityName].cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using MediatR;

public static class Delete[EntityName]
{
    public sealed record Command(string Id) : IRequest;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command>
    {
        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            var entity = await dbContext.[EntityName]s.GetById(request.Id, cancellationToken);

            dbContext.[EntityName]s.Remove(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
```

**File:** `Domain/[EntityName]s/Features/Get[EntityName].cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using FullstackTemplate.Server.Domain.[EntityName]s.Dtos;
using FullstackTemplate.Server.Domain.[EntityName]s.Mappings;
using MediatR;

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

**File:** `Domain/[EntityName]s/Features/Get[EntityName]List.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using FullstackTemplate.Server.Domain.[EntityName]s.Dtos;
using FullstackTemplate.Server.Domain.[EntityName]s.Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QueryKit;

public static class Get[EntityName]List
{
    public sealed record Query([EntityName]ParametersDto Parameters) : IRequest<PagedList<[EntityName]Dto>>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Query, PagedList<[EntityName]Dto>>
    {
        public async Task<PagedList<[EntityName]Dto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var queryKitConfig = new CustomQueryKitConfiguration();

            var query = dbContext.[EntityName]s
                .AsNoTracking()
                .ApplyQueryKitFilter(request.Parameters.Filters, queryKitConfig)
                .ApplyQueryKitSort(request.Parameters.SortOrder, queryKitConfig);

            var dtos = query.To[EntityName]DtoQueryable();

            return await PagedList<[EntityName]Dto>.CreateAsync(
                dtos,
                request.Parameters.PageNumber,
                request.Parameters.PageSize,
                cancellationToken);
        }
    }
}
```

## 7. Controller

**File:** `Controllers/v1/[EntityName]sController.cs`

```csharp
namespace FullstackTemplate.Server.Controllers.v1;

using Asp.Versioning;
using FullstackTemplate.Server.Domain.[EntityName]s.Dtos;
using FullstackTemplate.Server.Domain.[EntityName]s.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v{v:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public sealed class [EntityName]sController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Gets a single [EntityName] by ID.
    /// </summary>
    [Authorize]
    [HttpGet("{id}", Name = "Get[EntityName]")]
    [ProducesResponseType(typeof([EntityName]Dto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<[EntityName]Dto>> Get[EntityName](string id)
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

        Response.AddPaginationHeader(result.Pagination);

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
    [HttpPut("{id}", Name = "Update[EntityName]")]
    [ProducesResponseType(typeof([EntityName]Dto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<[EntityName]Dto>> Update[EntityName](
        string id,
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
    [HttpDelete("{id}", Name = "Delete[EntityName]")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete[EntityName](string id)
    {
        var command = new Delete[EntityName].Command(id);
        await mediator.Send(command);
        return NoContent();
    }
}
```

## 8. Entity Configuration

**File:** `Databases/EntityConfigurations/[EntityName]Configuration.cs`

```csharp
namespace FullstackTemplate.Server.Databases.EntityConfigurations;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.[EntityName]s;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class [EntityName]Configuration : IEntityTypeConfiguration<[EntityName]>
{
    public void Configure(EntityTypeBuilder<[EntityName]> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        // Each ID foreign key gets the same length and collation as the ID that it points to.
        // builder.Property(e => e.TenantId)
        //     .IsRequired()
        //     .HasMaxLength(EntityId.MaxLength)
        //     .UseCollation(EntityId.Collation);
        //
        // builder.HasOne<Tenant>()
        //     .WithMany()
        //     .HasForeignKey(e => e.TenantId)
        //     .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        // Indexes
        builder.HasIndex(e => e.Name);

        // Value object configurations
        // builder.OwnsOne(e => e.Email, email =>
        // {
        //     email.Property(e => e.Value)
        //         .HasColumnName("email")
        //         .HasMaxLength(320);
        // });

        // builder.OwnsOne(e => e.Status, status =>
        // {
        //     status.Property(s => s.Value)
        //         .HasColumnName("status")
        //         .HasMaxLength(50)
        //         .IsRequired();
        // });
    }
}
```

## 9. Infrastructure Updates

**Add DbSet to `AppDbContext.cs`:**

```csharp
public DbSet<[EntityName]> [EntityName]s => Set<[EntityName]>();
```

**Ensure configurations are applied in `OnModelCreating`:**

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Apply all IEntityTypeConfiguration implementations from assembly
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    modelBuilder.FilterSoftDeletedRecords();
}
```

## Required Dependencies

Ensure these packages are installed:

```xml
<PackageReference Include="MediatR" />
<PackageReference Include="Riok.Mapperly" />
<PackageReference Include="FluentValidation" />
<PackageReference Include="QueryKit" />
```

## Supporting Infrastructure

### DomainEvent Base Record

```csharp
namespace FullstackTemplate.Server.Domain;

using MediatR;

public abstract record DomainEvent : INotification;
```

### PagedList

```csharp
namespace FullstackTemplate.Server.Resources;

public class PagedList<T>
{
    public IReadOnlyList<T> Items { get; }
    public Pagination Pagination { get; }

    public static async Task<PagedList<T>> CreateAsync(
        IQueryable<T> source,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Implementation
    }
}
```

---

# Extending Existing Entities

## Adding a New Property

### Step 1: Add to Domain Entity

```csharp
// In [EntityName].cs - add property with private setter
public string PhoneNumber { get; private set; }

// Or for value objects
public EmailAddress Email { get; private set; } = default!;
```

### Step 2: Update Factory Method (if set at creation)

```csharp
public static [EntityName] Create([EntityName]ForCreation forCreation)
{
    var entity = new [EntityName]
    {
        // ... existing properties
        PhoneNumber = forCreation.PhoneNumber,
        Email = EmailAddress.Of(forCreation.Email),
    };
    // ...
}
```

### Step 3: Update Update Method (if updatable)

```csharp
public [EntityName] Update([EntityName]ForUpdate forUpdate)
{
    // ... existing updates
    PhoneNumber = forUpdate.PhoneNumber;
    Email = EmailAddress.Of(forUpdate.Email);

    QueueDomainEvent(new [EntityName]Updated(Id));
    return this;
}
```

### Step 4: Update Internal Models

```csharp
// [EntityName]ForCreation.cs
public sealed record [EntityName]ForCreation
{
    // ... existing properties
    public string? PhoneNumber { get; init; }
    public string Email { get; init; } = default!;
}

// [EntityName]ForUpdate.cs
public sealed record [EntityName]ForUpdate
{
    // ... existing properties
    public string? PhoneNumber { get; init; }
    public string Email { get; init; } = default!;
}
```

### Step 5: Update DTOs

```csharp
// [EntityName]Dto.cs
public sealed record [EntityName]Dto
{
    // ... existing properties
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
}

// [EntityName]ForCreationDto.cs
public sealed record [EntityName]ForCreationDto
{
    // ... existing properties
    public string? PhoneNumber { get; init; }
    public string Email { get; init; } = default!;
}

// [EntityName]ForUpdateDto.cs
public sealed record [EntityName]ForUpdateDto
{
    // ... existing properties
    public string? PhoneNumber { get; init; }
    public string Email { get; init; } = default!;
}
```

### Step 6: Update Mapper (for value objects)

```csharp
[Mapper]
public static partial class [EntityName]Mapper
{
    // ... existing mappings

    // Add custom mapping for value objects
    private static string? MapEmail(EmailAddress? email) => email?.Value;
}
```

### Step 7: Update Entity Configuration

```csharp
public sealed class [EntityName]Configuration : IEntityTypeConfiguration<[EntityName]>
{
    public void Configure(EntityTypeBuilder<[EntityName]> builder)
    {
        // ... existing configuration

        // Simple property
        builder.Property(e => e.PhoneNumber)
            .HasMaxLength(20);

        // Value object
        builder.OwnsOne(e => e.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("email")
                .HasMaxLength(320);
        });

        // Add index if frequently queried
        builder.HasIndex(e => e.PhoneNumber);
    }
}
```

---

## Adding a New Feature (Business Action)

### Business Action Command Template

**File:** `Domain/[EntityName]s/Features/[Action][EntityName].cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using FullstackTemplate.Server.Domain.[EntityName]s.Dtos;
using FullstackTemplate.Server.Domain.[EntityName]s.Mappings;
using MediatR;

public static class [Action][EntityName]
{
    public sealed record Command(string Id) : IRequest<[EntityName]Dto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, [EntityName]Dto>
    {
        public async Task<[EntityName]Dto> Handle(Command request, CancellationToken cancellationToken)
        {
            var entity = await dbContext.[EntityName]s.GetById(request.Id, cancellationToken);

            entity.[Action]();  // Call domain method

            await dbContext.SaveChangesAsync(cancellationToken);

            return entity.To[EntityName]Dto();
        }
    }
}
```

### Business Action with Parameters Template

**File:** `Domain/[EntityName]s/Features/[Action][EntityName].cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using FullstackTemplate.Server.Domain.[EntityName]s.Dtos;
using FullstackTemplate.Server.Domain.[EntityName]s.Mappings;
using MediatR;

public static class [Action][EntityName]
{
    public sealed record Command(string Id, [Action][EntityName]Dto Dto) : IRequest<[EntityName]Dto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, [EntityName]Dto>
    {
        public async Task<[EntityName]Dto> Handle(Command request, CancellationToken cancellationToken)
        {
            var entity = await dbContext.[EntityName]s.GetById(request.Id, cancellationToken);

            entity.[Action](request.Dto.Parameter1, request.Dto.Parameter2);

            await dbContext.SaveChangesAsync(cancellationToken);

            return entity.To[EntityName]Dto();
        }
    }
}
```

### Domain Event Template

**File:** `Domain/[EntityName]s/DomainEvents/[EntityName][Action].cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.DomainEvents;

public sealed record [EntityName][Action](
    string [EntityName]Id,
    DateTimeOffset [Action]At) : DomainEvent; // Add any other relevant data
```

If the action takes a reason, put the reason in the event. The event must be self-contained.

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.DomainEvents;

public sealed record [EntityName][Action](
    string [EntityName]Id,
    string Reason,
    DateTimeOffset [Action]At) : DomainEvent;
```

### Domain Method Template

```csharp
// Add to [EntityName].cs

/// <summary>
/// [Description of the action].
/// </summary>
public [EntityName] [Action]()
{
    // Guard against invalid state
    Guard[Condition]("[Error message]");

    // Perform state changes
    Status = [EntityName]Status.[NewStatus]();
    [Action]At = DateTimeOffset.UtcNow;

    // Queue domain event
    QueueDomainEvent(new [EntityName][Action](Id, [Action]At));

    return this;
}

/// <summary>
/// [Description of the action] with parameters.
/// </summary>
public [EntityName] [Action](string reason)
{
    Guard[Condition]("[Error message]");

    Status = [EntityName]Status.[NewStatus]();
    [Action]Reason = reason;

    QueueDomainEvent(new [EntityName][Action](Id, reason, DateTimeOffset.UtcNow));

    return this;
}
```

### Controller Endpoint Template

```csharp
// Add to [EntityName]sController.cs

/// <summary>
/// [Description of the action].
/// </summary>
[Authorize]
[HttpPost("{id}/[action-route]", Name = "[Action][EntityName]")]
[ProducesResponseType(typeof([EntityName]Dto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<ActionResult<[EntityName]Dto>> [Action][EntityName](string id)
{
    var command = new [Action][EntityName].Command(id);
    var result = await mediator.Send(command);
    return Ok(result);
}

/// <summary>
/// [Description of the action] with parameters.
/// </summary>
[Authorize]
[HttpPost("{id}/[action-route]", Name = "[Action][EntityName]")]
[ProducesResponseType(typeof([EntityName]Dto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<ActionResult<[EntityName]Dto>> [Action][EntityName](
    string id,
    [FromBody] [Action][EntityName]Dto dto)
{
    var command = new [Action][EntityName].Command(id, dto);
    var result = await mediator.Send(command);
    return Ok(result);
}
```

---

## Adding a Custom Query

### Custom Query Template

**File:** `Domain/[EntityName]s/Features/Get[EntityName]sBy[Criteria].cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using FullstackTemplate.Server.Domain.[EntityName]s.Dtos;
using FullstackTemplate.Server.Domain.[EntityName]s.Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;

public static class Get[EntityName]sBy[Criteria]
{
    public sealed record Query(string [Criteria]Id) : IRequest<IReadOnlyList<[EntityName]Dto>>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Query, IReadOnlyList<[EntityName]Dto>>
    {
        public async Task<IReadOnlyList<[EntityName]Dto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var entities = await dbContext.[EntityName]s
                .AsNoTracking()
                .Where(e => e.[Criteria]Id == request.[Criteria]Id)
                .To[EntityName]DtoQueryable()
                .ToListAsync(cancellationToken);

            return entities;
        }
    }
}
```

### Controller Endpoint for Custom Query

```csharp
// Add to controller (could be [EntityName]sController or [Criteria]Controller)

/// <summary>
/// Gets all [EntityName]s for a specific [Criteria].
/// </summary>
[Authorize]
[HttpGet("by-[criteria]/{[criteria]Id}", Name = "Get[EntityName]sBy[Criteria]")]
[ProducesResponseType(typeof(IReadOnlyList<[EntityName]Dto>), StatusCodes.Status200OK)]
public async Task<ActionResult<IReadOnlyList<[EntityName]Dto>>> Get[EntityName]sBy[Criteria](string [criteria]Id)
{
    var query = new Get[EntityName]sBy[Criteria].Query([criteria]Id);
    var result = await mediator.Send(query);
    return Ok(result);
}
```
