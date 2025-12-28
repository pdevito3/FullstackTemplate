namespace FullstackTemplate.Server.Domain.Users.Controllers.v1;

using Asp.Versioning;
using Dtos;
using Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Resources;
using Resources.Extensions;

[ApiController]
[Route("api/v{v:apiVersion}/users")]
[ApiVersion("1.0")]
public sealed class UsersController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Gets a single User by ID.
    /// </summary>
    [Authorize]
    [HttpGet("{id:guid}", Name = "GetUser")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetUser(Guid id)
    {
        var query = new GetUser.Query(id);
        var result = await mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Gets a User by their IDP identifier (sub claim).
    /// </summary>
    [Authorize]
    [HttpGet("by-identifier/{identifier}", Name = "GetUserByIdentifier")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetUserByIdentifier(string identifier)
    {
        var query = new GetUserByIdentifier.Query(identifier);
        var result = await mediator.Send(query);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    /// <summary>
    /// Gets a paginated list of Users.
    /// </summary>
    [Authorize]
    [HttpGet(Name = "GetUserList")]
    [ProducesResponseType(typeof(PagedList<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedList<UserDto>>> GetUserList(
        [FromQuery] UserParametersDto parameters)
    {
        var query = new GetUserList.Query(parameters);
        var result = await mediator.Send(query);

        Response.AddPaginationHeader(result);

        return Ok(result);
    }

    /// <summary>
    /// Creates a new User.
    /// </summary>
    [Authorize]
    [HttpPost(Name = "AddUser")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> AddUser(
        [FromBody] UserForCreationDto dto)
    {
        var command = new AddUser.Command(dto);
        var result = await mediator.Send(command);

        return CreatedAtRoute("GetUser",
            new { id = result.Id },
            result);
    }

    /// <summary>
    /// Updates an existing User.
    /// </summary>
    [Authorize]
    [HttpPut("{id:guid}", Name = "UpdateUser")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> UpdateUser(
        Guid id,
        [FromBody] UserForUpdateDto dto)
    {
        var command = new UpdateUser.Command(id, dto);
        var result = await mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Deletes a User.
    /// </summary>
    [Authorize]
    [HttpDelete("{id:guid}", Name = "DeleteUser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteUser(Guid id)
    {
        var command = new DeleteUser.Command(id);
        await mediator.Send(command);
        return NoContent();
    }
}
