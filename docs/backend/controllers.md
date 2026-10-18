# Controllers

A controller gets an HTTP request, sends a MediatR request, and returns the HTTP response. It has no other logic. The example is `Domain/Users/Controllers/v1/UsersController.cs`.

## Rules

1. Put the controller in `Domain/[EntityName]s/Controllers/v{n}/`.
2. Make the controller `sealed`. Inject only `IMediator`, with a primary constructor.
3. Do not inject `AppDbContext`.
4. Do not catch exceptions. The global handler maps them to problem details. See [backend.md](../backend.md#error-responses).
5. Give each action `[Authorize]`, or `[AllowAnonymous]` when the endpoint is public.
6. Give each action a route `Name`. `CreatedAtRoute` uses the name.
7. Give each action an XML `<summary>` and a `[ProducesResponseType]` for each status that it can return.
8. Declare `ValidationProblemDetails` as the type of each 400, 422, and 409. See [backend.md](../backend.md#error-responses) for the statuses that each action declares.
9. Do not add a route constraint to an entity ID. When the ID is malformed, no entity has it, so the API returns 404.
10. Do not add the `X-Pagination` header in the controller. `PaginationHeaderFilter` adds it to each `PagedList<T>` result.
11. End the name of each list endpoint in `List`, and use `List` for no other endpoint. The frontend client of a list endpoint returns the full response, so that the caller can read the header. If a name breaks this rule, the Debug build fails.

## Example

```csharp
[ApiController]
[Route("api/v{v:apiVersion}/users")]
[ApiVersion("1.0")]
public sealed class UsersController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Gets a single User by ID.
    /// </summary>
    [Authorize]
    [HttpGet("{id}", Name = "GetUser")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetUser(string id)
    {
        var result = await mediator.Send(new GetUser.Query(id));
        return Ok(result);
    }

    /// <summary>
    /// Gets a paginated list of Users.
    /// </summary>
    [Authorize]
    [HttpGet(Name = "GetUserList")]
    [ProducesResponseType(typeof(PagedList<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedList<UserDto>>> GetUserList([FromQuery] UserParametersDto parameters)
    {
        var result = await mediator.Send(new GetUserList.Query(parameters));
        return Ok(result);
    }

    /// <summary>
    /// Creates a new User.
    /// </summary>
    [Authorize]
    [HttpPost(Name = "AddUser")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> AddUser([FromBody] UserForCreationDto dto)
    {
        var result = await mediator.Send(new AddUser.Command(dto));
        return CreatedAtRoute("GetUser", new { id = result.Id }, result);
    }
}
```

## HTTP methods

| Operation | Method | Route | Success status |
|-----------|--------|-------|----------------|
| Get one | GET | `/{id}` | 200 |
| Get a list | GET | `/` | 200, with the `X-Pagination` header from `PaginationHeaderFilter` |
| Create | POST | `/` | 201, with `CreatedAtRoute` |
| Update | PUT | `/{id}` | 200 |
| Delete | DELETE | `/{id}` | 204 |
| Business action | POST or PUT | `/{id}/action`, for example `/{id}/role` | 200 |

## Versions

Add a new version when a change breaks callers: a removed or renamed DTO property, a changed type, a changed route, or a removed endpoint. Put the new controller in `Controllers/v2/` with `[ApiVersion("2.0")]`. Then add the version to `SupportedVersions`. See [backend.md](../backend.md#api-versioning).
