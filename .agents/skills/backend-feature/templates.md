# Backend Feature Templates

Complete code templates for generating a vertical slice feature. Replace `[EntityName]` with the actual entity name (e.g., `Customer`, `Order`). Replace `[entity_name]`, `[action_name]`, and `[criteria_name]` with the snake_case names (e.g., `customer`, `change_status`, `sales_region`), which the query tags and the span tags use. Replace `[ActionPastTense]` with the action in the past tense for the log (e.g., `Archived`).

## 1. Domain Entity

**File:** `Domain/[EntityName]s/[EntityName].cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s;

using FullstackTemplate.Server.Domain.[EntityName]s.DomainEvents;
using FullstackTemplate.Server.Domain.[EntityName]s.Models;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Exceptions;

// A root has a tenant of its own, so its audit entries name their tenant. A child of a root is not ITenantable.
public class [EntityName] : BaseEntity, ITenantable
{
    // A short, unique type prefix. The ID is "[prefix]_{uuidv7}".
    public const string IdPrefix = "[prefix]";

    // A max length for each text value that a request sets. The entity configuration uses the same constants.
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    [NotSensitive] public string TenantId { get; private set; } = default!;

    // Properties with private setters. Mark each property for the audit log, except a flag, an enum, a Guid, an
    // integer, and an ID foreign key.
    [NotSensitive] public string Name { get; private set; } = default!;
    [Sensitive] public string? Description { get; private set; }

    // Other ID foreign keys
    // public string OtherId { get; private set; } = default!;

    // Value object properties
    // [Sensitive] public EmailAddress Email { get; private set; } = default!;
    // [NotSensitive] public [EntityName]Status Status { get; private set; } = default!;

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
            TenantId = forCreation.TenantId,
            Name = forCreation.Name,
            Description = forCreation.Description,
        };
        // A test can give a fixed ID. Set it before you add child rows, so that they get the final ID.
        if (forCreation.Id is not null)
            entity.SetId(forCreation.Id);

        Validate[EntityName](entity);
        entity.QueueDomainEvent(new [EntityName]Created(entity.Id));

        return entity;
    }

    /// <summary>
    /// Updates the [EntityName].
    /// </summary>
    public [EntityName] Update([EntityName]ForUpdate forUpdate)
    {
        // The caller edited the version that it loaded. A newer version gives 409
        GuardVersion(forUpdate.Version);
        Name = forUpdate.Name;
        Description = forUpdate.Description;

        Validate[EntityName](this);
        QueueDomainEvent(new [EntityName]Updated(Id));

        return this;
    }

    private static void Validate[EntityName]([EntityName] entity)
    {
        ValidationException.ThrowWhenNullOrWhitespace(entity.Name, "name", "Please provide a name.");
        ValidationException.ThrowWhenLongerThan(entity.Name, NameMaxLength, "name",
            $"A name can have no more than {NameMaxLength} characters.");
        ValidationException.ThrowWhenHasCharacterPostgresCannotStore(entity.Name, "name",
            "A name has a character that is not allowed.");

        if (entity.Description is not null)
        {
            ValidationException.ThrowWhenLongerThan(entity.Description, DescriptionMaxLength, "description",
                $"A description can have no more than {DescriptionMaxLength} characters.");
            ValidationException.ThrowWhenHasCharacterPostgresCannotStore(entity.Description, "description",
                "A description has a character that is not allowed.");
        }

        // Check the prefix of each ID foreign key.
        ValidationException.ThrowWhenNullOrWhitespace(entity.TenantId, "tenantId", "Please provide a tenant.");
        ValidationException.Must(EntityId.IsValid(entity.TenantId, Tenant.IdPrefix), "tenantId", "Please provide a valid tenant ID.");
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

public sealed record [EntityName]Created(string [EntityName]Id) : DomainEvent;
```

**File:** `Domain/[EntityName]s/DomainEvents/[EntityName]Updated.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.DomainEvents;

public sealed record [EntityName]Updated(string Id) : DomainEvent;
```

An event carries IDs and values, never an entity. Do not add a deleted event. The context does soft deletes, and no handler needs the event. Add a handler only when a feature needs one. See `docs/backend/domain-events.md`.

## 3. DTOs

**File:** `Domain/[EntityName]s/Dtos/[EntityName]Dto.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Dtos;

public sealed record [EntityName]Dto
{
    public string Id { get; init; } = default!;
    public uint Version { get; init; }
    public string TenantId { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public DateTimeOffset? LastModifiedOn { get; init; }
}
```

**File:** `Domain/[EntityName]s/Dtos/[EntityName]ForCreationDto.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Dtos;

using Resources;

public sealed record [EntityName]ForCreationDto
{
    // A missing value is a 400. An empty value goes to the domain rules, which give a 422
    [RequiredField]
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
}
```

**File:** `Domain/[EntityName]s/Dtos/[EntityName]ForUpdateDto.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Dtos;

using Resources;

public sealed record [EntityName]ForUpdateDto
{
    // The version from the [EntityName]Dto that the caller edited. A missing value is a 400
    public required uint Version { get; init; }

    // A missing value is a 400. An empty value goes to the domain rules, which give a 422
    [RequiredField]
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
}
```

**File:** `Domain/[EntityName]s/Dtos/[EntityName]ParametersDto.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Dtos;

using Resources;

public sealed class [EntityName]ParametersDto : BasePaginationParameters
{
    public string? Filters { get; set; }

    public string? SortOrder { get; set; }

    /// <summary>
    /// Makes the parameters from the values of a caller. A null value keeps the default. An empty filter or sort
    /// order means none.
    /// </summary>
    public static [EntityName]ParametersDto From(int? pageNumber, int? pageSize, string? filters, string? sortOrder)
    {
        var parameters = new [EntityName]ParametersDto
        {
            Filters = string.IsNullOrEmpty(filters) ? null : filters,
            SortOrder = string.IsNullOrEmpty(sortOrder) ? null : sortOrder,
        };
        parameters.SetPage(pageNumber, pageSize);
        return parameters;
    }
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
    public string TenantId { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
}
```

**File:** `Domain/[EntityName]s/Models/[EntityName]ForUpdate.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Models;

public sealed record [EntityName]ForUpdate
{
    public uint Version { get; init; }
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

    // The API does not take an ID. Only tests set a fixed ID on the model. The handler decides the tenant.
    [MapperIgnoreTarget(nameof([EntityName]ForCreation.Id))]
    public static partial [EntityName]ForCreation To[EntityName]ForCreation(
        this [EntityName]ForCreationDto dto, string tenantId);

    public static partial [EntityName]ForUpdate To[EntityName]ForUpdate(
        this [EntityName]ForUpdateDto dto);

    // Add custom mappings for value objects here
    // private static string MapStatus([EntityName]Status status) => status.Value;
}
```

## 6. Features

Each command checks a permission first (see `docs/backend/features-and-cqrs.md#permission-checks`). The templates use `Permission.Manage[EntityName]s()`. Add it to `Domain/Users/Permission.cs` with its `Permission.Names` constant, and add a migration that gives it to the admins that exist (see `docs/backend.md#add-a-permission`). A query has no permission check, unless it reads data that needs a permission, such as the other users or the agents.

**File:** `Domain/[EntityName]s/Features/Add[EntityName]/Add[EntityName].cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using System.Diagnostics;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.[EntityName]s.Dtos;
using FullstackTemplate.Server.Domain.[EntityName]s.Mappings;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources;
using MediatR;
using Serilog;

public static class Add[EntityName]
{
    public sealed record Command([EntityName]ForCreationDto Dto) : IRequest<[EntityName]Dto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Command, [EntityName]Dto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<[EntityName]Dto> Handle(Command request, CancellationToken cancellationToken)
        {
            await permissionGuard.RequireAsync(Permission.Manage[EntityName]s(), cancellationToken);

            // The caller adds to its own tenant. AddUser shows a request that names the tenant.
            var tenantId = dbContext.CurrentTenantScope.TenantId
                ?? throw new ForbiddenAccessException("You can add a [EntityName] only to your own tenant.");
            var forCreation = request.Dto.To[EntityName]ForCreation(tenantId);
            var entity = [EntityName].Create(forCreation);
            Activity.Current?.SetTag("app.[entity_name]_id", entity.Id);

            await dbContext.[EntityName]s.AddAsync(entity, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Here().Information("Added [EntityName] {[EntityName]Id}", entity.Id);

            return entity.To[EntityName]Dto();
        }
    }
}
```

**File:** `Domain/[EntityName]s/Features/Update[EntityName]/Update[EntityName].cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using System.Diagnostics;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.[EntityName]s.Dtos;
using FullstackTemplate.Server.Domain.[EntityName]s.Mappings;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources;
using MediatR;
using Serilog;

public static class Update[EntityName]
{
    public sealed record Command(string Id, [EntityName]ForUpdateDto Dto) : IRequest<[EntityName]Dto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Command, [EntityName]Dto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<[EntityName]Dto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.[entity_name]_id", request.Id);

            await permissionGuard.RequireAsync(Permission.Manage[EntityName]s(), cancellationToken);

            var entity = await dbContext.[EntityName]s
                .TagWithOperationCallSite("[entity_name]s.update.load")
                .GetById(request.Id, cancellationToken);

            var forUpdate = request.Dto.To[EntityName]ForUpdate();
            entity.Update(forUpdate);

            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Here().Information("Updated [EntityName] {[EntityName]Id}", entity.Id);

            return entity.To[EntityName]Dto();
        }
    }
}
```

**File:** `Domain/[EntityName]s/Features/Delete[EntityName]/Delete[EntityName].cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using System.Diagnostics;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources;
using MediatR;
using Serilog;

public static class Delete[EntityName]
{
    public sealed record Command(string Id) : IRequest;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Command>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.[entity_name]_id", request.Id);

            await permissionGuard.RequireAsync(Permission.Manage[EntityName]s(), cancellationToken);

            var entity = await dbContext.[EntityName]s
                .TagWithOperationCallSite("[entity_name]s.delete.load")
                .GetById(request.Id, cancellationToken);

            dbContext.[EntityName]s.Remove(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Here().Information("Deleted [EntityName] {[EntityName]Id}", entity.Id);
        }
    }
}
```

**File:** `Domain/[EntityName]s/Features/Get[EntityName]/Get[EntityName].cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using System.Diagnostics;
using FullstackTemplate.Server.Databases;
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
            Activity.Current?.SetTag("app.[entity_name]_id", request.Id);

            var entity = await dbContext.[EntityName]s
                .TagWithOperationCallSite("[entity_name]s.get_by_id")
                .GetById(request.Id, cancellationToken);
            return entity.To[EntityName]Dto();
        }
    }
}
```

**File:** `Domain/[EntityName]s/Features/Get[EntityName]List/Get[EntityName]List.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.[EntityName]s.Dtos;
using FullstackTemplate.Server.Domain.[EntityName]s.Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using FullstackTemplate.Server.Resources;

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
                .TagWithOperationCallSite("[entity_name]s.list")
                .AsNoTracking()
                .To[EntityName]DtoQueryable();

            if (!string.IsNullOrWhiteSpace(request.Parameters.Filters))
                query = query.ApplyFilters(request.Parameters.Filters, queryKitConfig);

            // Paging needs a fixed order, or a row can show on two pages or on none
            query = string.IsNullOrWhiteSpace(request.Parameters.SortOrder)
                ? query.OrderBy(e => e.Name).ThenBy(e => e.Id)
                : query.ApplySortOrder(request.Parameters.SortOrder, queryKitConfig).ThenBy(e => e.Id);

            return await PagedList.CreateAsync(
                query,
                request.Parameters.PageNumber,
                request.Parameters.PageSize,
                cancellationToken);
        }
    }
}
```

## 7. Endpoints

Each feature folder gets one `IEndpoint`. `EndpointDiscovery` finds it at startup and maps it on `api/v{version:apiVersion}`. See `docs/backend/endpoints.md`.

**File:** `Domain/[EntityName]s/Features/Get[EntityName]/Get[EntityName].Http.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class Get[EntityName]Endpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/[entityName]s/{id}", Handle)
        .WithName("Get[EntityName]")
        .WithTags("[EntityName]s")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status404NotFound);

    /// <summary>
    /// Gets a single [EntityName] by ID.
    /// </summary>
    public static async Task<Ok<[EntityName]Dto>> Handle(string id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Get[EntityName].Query(id), cancellationToken);
        return TypedResults.Ok(result);
    }
}
```

**File:** `Domain/[EntityName]s/Features/Get[EntityName]List/Get[EntityName]List.Http.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using System.ComponentModel.DataAnnotations;
using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources;
using Resources.Entrypoints.Http;

public sealed class Get[EntityName]ListEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/[entityName]s", Handle)
        .WithName("Get[EntityName]List")
        .WithTags("[EntityName]s")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem();

    /// <summary>
    /// Gets a paginated list of [EntityName]s.
    /// </summary>
    // One nullable parameter for each query value, with the camelCase name of the contract
    public static async Task<Ok<PagedList<[EntityName]Dto>>> Handle(
        string? filters,
        string? sortOrder,
        [Range(1, BasePaginationParameters.MaxPageNumber)] int? pageNumber,
        [Range(1, int.MaxValue)] int? pageSize,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var parameters = [EntityName]ParametersDto.From(pageNumber, pageSize, filters, sortOrder);

        var result = await mediator.Send(new Get[EntityName]List.Query(parameters), cancellationToken);
        return TypedResults.Ok(result);
    }
}
```

**File:** `Domain/[EntityName]s/Features/Add[EntityName]/Add[EntityName].Http.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class Add[EntityName]Endpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPost("/[entityName]s", Handle)
        .WithName("Add[EntityName]")
        .WithTags("[EntityName]s")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem()
        // The handler checks a permission, so it can return 403
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        // Declare a 409 if this write can add or change a unique value
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Creates a new [EntityName].
    /// </summary>
    public static async Task<CreatedAtRoute<[EntityName]Dto>> Handle(
        [EntityName]ForCreationDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Add[EntityName].Command(dto), cancellationToken);
        return TypedResults.CreatedAtRoute(result, "Get[EntityName]", new { id = result.Id });
    }
}
```

**File:** `Domain/[EntityName]s/Features/Update[EntityName]/Update[EntityName].Http.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class Update[EntityName]Endpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPut("/[entityName]s/{id}", Handle)
        .WithName("Update[EntityName]")
        .WithTags("[EntityName]s")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        // The handler checks a permission, so it can return 403
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        // A stale version, a lost race, or a duplicate unique value
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Updates an existing [EntityName].
    /// </summary>
    public static async Task<Ok<[EntityName]Dto>> Handle(
        string id,
        [EntityName]ForUpdateDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Update[EntityName].Command(id, dto), cancellationToken);
        return TypedResults.Ok(result);
    }
}
```

**File:** `Domain/[EntityName]s/Features/Delete[EntityName]/Delete[EntityName].Http.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class Delete[EntityName]Endpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapDelete("/[entityName]s/{id}", Handle)
        .WithName("Delete[EntityName]")
        .WithTags("[EntityName]s")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        // The handler checks a permission, so it can return 403
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        // The delete is an update of the row, so it can lose a race with another save
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Deletes a [EntityName].
    /// </summary>
    public static async Task<NoContent> Handle(string id, IMediator mediator, CancellationToken cancellationToken)
    {
        await mediator.Send(new Delete[EntityName].Command(id), cancellationToken);
        return TypedResults.NoContent();
    }
}
```

### MCP Tool

Add this file only when an MCP caller needs the feature. Each tool has exactly one `[ToolAccess]`, or the server does not start. It decides who sees the tool in `tools/list` and who can call it. The handler still makes its own check.

- `[ToolAccess(Permission.Names.X)]`: the same permission that the handler requires.
- `[ToolAccess(ToolAccessKind.Read)]`: a read with no permission. The tenant filter decides the rows.
- `[ToolAccess(ToolAccessKind.ManageAllTenants)]`: only a caller that manages all tenants.

**File:** `Domain/[EntityName]s/Features/Update[EntityName]/Update[EntityName].Mcp.cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using System.ComponentModel;
using Dtos;
using MediatR;
using ModelContextProtocol.Server;
using Resources.Entrypoints.Mcp;
using Users;

[McpServerToolType]
public static class Update[EntityName]Tool
{
    [McpServerTool(Name = "update_[entity_name]", Destructive = true, Idempotent = true, UseStructuredContent = true)]
    [ToolAccess(Permission.Names.Manage[EntityName]s)]
    [Description("Changes a [EntityName]. The caller needs the manage_[entity_name]s permission in the tenant.")]
    public static Task<[EntityName]Dto> Handle(
        IMediator mediator,
        [Description("The [EntityName] ID.")] string id,
        [Description("The new name.")] string name,
        CancellationToken cancellationToken)
        => mediator.Send(new Update[EntityName].Command(id, new [EntityName]ForUpdateDto { Name = name }), cancellationToken);
}
```

## 8. Entity Configuration

**File:** `Databases/EntityConfigurations/[EntityName]Configuration.cs`

```csharp
namespace FullstackTemplate.Server.Databases.EntityConfigurations;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.[EntityName]s;
using FullstackTemplate.Server.Domain.Tenants;
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
        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength([EntityName].NameMaxLength);

        builder.Property(e => e.Description)
            .HasMaxLength([EntityName].DescriptionMaxLength);

        // Indexes
        builder.HasIndex(e => e.Name);

        // Value object configurations. The audit log reads a value object only as a ComplexProperty, not OwnsOne.
        // builder.ComplexProperty(e => e.Email, email =>
        // {
        //     email.Property(e => e.Value)
        //         .HasColumnName("email")
        //         .HasMaxLength(320);
        // });

        // builder.ComplexProperty(e => e.Status, status =>
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

public abstract record DomainEvent;
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
// In [EntityName].cs - add property with private setter and its audit mark
[Sensitive] public string PhoneNumber { get; private set; }

// Or for value objects
[Sensitive] public EmailAddress Email { get; private set; } = default!;
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
        builder.ComplexProperty(e => e.Email, email =>
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

**File:** `Domain/[EntityName]s/Features/[Action][EntityName]/[Action][EntityName].cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using System.Diagnostics;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.[EntityName]s.Dtos;
using FullstackTemplate.Server.Domain.[EntityName]s.Mappings;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources;
using MediatR;
using Serilog;

public static class [Action][EntityName]
{
    public sealed record Command(string Id) : IRequest<[EntityName]Dto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Command, [EntityName]Dto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<[EntityName]Dto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.[entity_name]_id", request.Id);

            await permissionGuard.RequireAsync(Permission.Manage[EntityName]s(), cancellationToken);

            var entity = await dbContext.[EntityName]s
                .TagWithOperationCallSite("[entity_name]s.[action_name].load")
                .GetById(request.Id, cancellationToken);

            entity.[Action]();  // Call domain method

            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Here().Information("[ActionPastTense] [EntityName] {[EntityName]Id}", entity.Id);

            return entity.To[EntityName]Dto();
        }
    }
}
```

### Business Action with Parameters Template

**File:** `Domain/[EntityName]s/Features/[Action][EntityName]/[Action][EntityName].cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using System.Diagnostics;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.[EntityName]s.Dtos;
using FullstackTemplate.Server.Domain.[EntityName]s.Mappings;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources;
using MediatR;
using Serilog;

public static class [Action][EntityName]
{
    public sealed record Command(string Id, [Action][EntityName]Dto Dto) : IRequest<[EntityName]Dto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Command, [EntityName]Dto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<[EntityName]Dto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.[entity_name]_id", request.Id);

            await permissionGuard.RequireAsync(Permission.Manage[EntityName]s(), cancellationToken);

            var entity = await dbContext.[EntityName]s
                .TagWithOperationCallSite("[entity_name]s.[action_name].load")
                .GetById(request.Id, cancellationToken);

            entity.[Action](request.Dto.Parameter1, request.Dto.Parameter2);

            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Here().Information("[ActionPastTense] [EntityName] {[EntityName]Id}", entity.Id);

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

### Endpoint Template

**File:** `Domain/[EntityName]s/Features/[Action][EntityName]/[Action][EntityName].Http.cs`

```csharp
public sealed class [Action][EntityName]Endpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPost("/[entityName]s/{id}/[action-route]", Handle)
        .WithName("[Action][EntityName]")
        .WithTags("[EntityName]s")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status404NotFound)
        // Declare a 400 if the action has a request body
        .ProducesValidationProblem()
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

    /// <summary>
    /// [Description of the action].
    /// </summary>
    // Add a [Action][EntityName]Dto parameter if the action has a request body
    public static async Task<Ok<[EntityName]Dto>> Handle(string id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new [Action][EntityName].Command(id), cancellationToken);
        return TypedResults.Ok(result);
    }
}
```

---

## Adding a Custom Query

### Custom Query Template

**File:** `Domain/[EntityName]s/Features/Get[EntityName]sBy[Criteria]/Get[EntityName]sBy[Criteria].cs`

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.Features;

using System.Diagnostics;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.[Criteria]s;
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
            Activity.Current?.SetTag("app.[criteria_name]_id", request.[Criteria]Id);

            // No row has an ID that is not valid, and Postgres cannot take an ID with a NUL as a parameter
            if (!EntityId.IsValid(request.[Criteria]Id, [Criteria].IdPrefix))
                return [];

            var entities = await dbContext.[EntityName]s
                .TagWithOperationCallSite("[entity_name]s.list_by_[criteria_name]")
                .AsNoTracking()
                .Where(e => e.[Criteria]Id == request.[Criteria]Id)
                .To[EntityName]DtoQueryable()
                .ToListAsync(cancellationToken);

            return entities;
        }
    }
}
```

### Endpoint for Custom Query

**File:** `Domain/[EntityName]s/Features/Get[EntityName]sBy[Criteria]/Get[EntityName]sBy[Criteria].Http.cs`

```csharp
public sealed class Get[EntityName]sBy[Criteria]Endpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/[entityName]s/by-[criteria]/{[criteria]Id}", Handle)
        .WithName("Get[EntityName]sBy[Criteria]")
        .WithTags("[EntityName]s")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization();

    /// <summary>
    /// Gets all [EntityName]s for a specific [Criteria].
    /// </summary>
    public static async Task<Ok<IReadOnlyList<[EntityName]Dto>>> Handle(
        string [criteria]Id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Get[EntityName]sBy[Criteria].Query([criteria]Id), cancellationToken);
        return TypedResults.Ok(result);
    }
}
```
