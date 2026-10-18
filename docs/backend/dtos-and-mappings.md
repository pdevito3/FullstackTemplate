# DTOs and mappings

A DTO is the API contract. A model is the input to an entity method. Mapperly generates the code that maps between them at compile time. The examples come from `Domain/Users/`.

## Files

```
Domain/[EntityName]s/
├── Dtos/
│   ├── [EntityName]Dto.cs              # Response
│   ├── [EntityName]ForCreationDto.cs   # POST body
│   ├── [EntityName]ForUpdateDto.cs     # PUT body
│   └── [EntityName]ParametersDto.cs    # List query string
├── Models/
│   ├── [EntityName]ForCreation.cs      # Input to Create
│   └── [EntityName]ForUpdate.cs        # Input to Update
└── Mappings/
    └── [EntityName]Mapper.cs
```

## Flow

```
Controller gets UserForCreationDto
  -> mapper makes UserForCreation
  -> User.Create(UserForCreation)
  -> mapper makes UserDto
  -> controller returns UserDto
```

## DTOs

1. Make each DTO a `sealed record` with `init` properties.
2. Give a required property `= default!`. Make an optional property nullable.
3. Use different DTOs for the response and for each request body.
4. Keep a DTO flat. Use a nested DTO only when the data is a real child, such as a list of line items.
5. Do not put an `Id`, audit fields, or internal data in a request DTO.
6. In a request DTO, mark each non-nullable value-type property (`Guid`, `int`, `bool`, and more) with `required`. Without it, a missing field binds to the default value, and the request gets a 422 from a domain rule, not a 400.

```csharp
public sealed record UserDto
{
    public string Id { get; init; } = default!;
    public string FirstName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Role { get; init; } = default!;
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
```

The parameters DTO is a class, because ASP.NET binds it from the query string. Give each query parameter a camelCase name with `[FromQuery(Name = ...)]`. This name is also the key in the `errors` of the problem. `BasePaginationParameters` names `pageNumber` and `pageSize`.

```csharp
public sealed class UserParametersDto : BasePaginationParameters
{
    [FromQuery(Name = "filters")]
    public string? Filters { get; set; }

    [FromQuery(Name = "sortOrder")]
    public string? SortOrder { get; set; }
}
```

## Models

A model is a `sealed record` with the same style as a DTO.

The creation model has a nullable `Id`. The test entity graph uses it to give an entity a fixed ID. A `ForCreationDto` never has an `Id`, and the mapper ignores the `Id` target.

```csharp
public sealed record UserForCreation
{
    /// <summary>A fixed ID with the prefix of the entity. When it is null, the entity makes a new ID.</summary>
    public string? Id { get; init; }
    public string FirstName { get; init; } = default!;
    public string Email { get; init; } = default!;
}
```

## Mapper

1. Make the mapper a `static partial class` with `[Mapper]`.
2. Write each mapping as an extension method, for example `user.ToUserDto()`.
3. Add an `IQueryable` projection for list queries. Mapperly turns it into one SQL query.
4. Map a value object to its primitive with a private method.
5. Ignore the audit fields and `DomainEvents` on the source of the DTO mapping.

```csharp
[Mapper]
public static partial class UserMapper
{
    [MapperIgnoreSource(nameof(User.CreatedBy))]
    [MapperIgnoreSource(nameof(User.LastModifiedBy))]
    [MapperIgnoreSource(nameof(User.CreatedOn))]
    [MapperIgnoreSource(nameof(User.LastModifiedOn))]
    [MapperIgnoreSource(nameof(User.IsDeleted))]
    [MapperIgnoreSource(nameof(User.DomainEvents))]
    [MapProperty(nameof(User.UserPermissions), nameof(UserDto.Permissions))]
    public static partial UserDto ToUserDto(this User user);

    public static partial IQueryable<UserDto> ToUserDtoQueryable(this IQueryable<User> queryable);

    [MapperIgnoreTarget(nameof(UserForCreation.Id))]
    public static partial UserForCreation ToUserForCreation(this UserForCreationDto dto, string tenantId);

    public static partial UserForUpdate ToUserForUpdate(this UserForUpdateDto dto);

    private static string MapEmail(EmailAddress email) => email.Value;
    private static string MapRole(UserRole role) => role.Value;
}
```

## Do not

- Do not return an entity from the API.
- Do not put business logic in a mapper. When a DTO needs a computed value, map it with an expression-bodied method, such as `UserMapper.MapFullName`. Then the value also translates to SQL in the list projection.
