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
Endpoint gets UserForCreationDto
  -> mapper makes UserForCreation
  -> User.Create(UserForCreation)
  -> mapper makes UserDto
  -> endpoint returns UserDto
```

## DTOs

1. Make each DTO a `sealed record` with `init` properties.
2. Give a required property `= default!`. Make an optional property nullable.
3. Use different DTOs for the response and for each request body.
4. Keep a DTO flat. Use a nested DTO only when the data is a real child, such as a list of line items.
5. Do not put an `Id`, `LastModifiedOn`, or internal data in a request DTO.
6. In a request DTO, mark each non-nullable value-type property (`Guid`, `int`, `bool`, and more) with `required`. Without it, a missing field binds to the default value, and the request gets a 422 from a domain rule, not a 400.
7. In a request DTO, give each non-nullable string property `[RequiredField]` from `Resources/RequiredFieldAttribute.cs`. This rule also applies to a child DTO, such as a DTO in a list of line items. A unit test fails if a non-nullable string property of a request DTO or of a child DTO has no `[RequiredField]`. `RequestValidationBehavior` does not read nullable annotations, so without it a missing field binds to null. With it, a missing field gets a 400 with a message that uses the JSON name of the field, and an empty string goes to the domain rules, which give a 422. The message names the field as the client sends it, for example "The firstName field is required.", not with a display name such as "First name", so that a client can match the message to its field.
8. Give the response DTO of an aggregate root `public uint Version { get; init; }`. Give each `[EntityName]ForUpdateDto` `public required uint Version { get; init; }`, and give its update model `public uint Version { get; init; }`. Mapperly maps it by name. The client sends back the version that it loaded, and the entity checks it (see [domain-entities.md](domain-entities.md#versions)). A unit test fails if a `ForUpdateDto` has no required `Version`.
9. Mark each string property that holds a name of a value object with a fixed list of names, such as `UserRole`, with `[NamesOf<UserRole>]`. Also mark a list of such names. The value object implements `IFixedNames`. The OpenAPI document then lists the names as an enum, so the generated SPA type is a union. If the Server renames a value, the SPA build fails.

```csharp
public sealed record UserDto
{
    public string Id { get; init; } = default!;
    public uint Version { get; init; }
    public string FirstName { get; init; } = default!;
    public string Email { get; init; } = default!;
    [NamesOf<UserRole>]
    public string Role { get; init; } = default!;
    [NamesOf<Permission>]
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
```

The parameters DTO is a class that derives from `BasePaginationParameters`. It has no transport attributes. The HTTP endpoint binds each query value to a handler parameter with the camelCase name, and copies it to the DTO (see [endpoints.md](endpoints.md#http)). The gRPC and MCP adapters fill the DTO from their own inputs.

```csharp
public sealed class UserParametersDto : BasePaginationParameters
{
    public string? Filters { get; set; }

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

Mapperly makes the map at build time. Do not write a reflection mapper. See [source-generators.md](source-generators.md).

1. Make the mapper a `static partial class` with `[Mapper]`.
2. Write each mapping as an extension method, for example `user.ToUserDto()`.
3. Add an `IQueryable` projection for list queries. Mapperly turns it into one SQL query.
4. Map a value object to its primitive with a private method.
5. Ignore `LastModifiedOn`, `IsDeleted`, and `DomainEvents` on the source of the DTO mapping.

```csharp
[Mapper]
public static partial class UserMapper
{
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
