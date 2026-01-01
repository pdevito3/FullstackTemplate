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

    /// <summary>
    /// Updates a User's role. This resets all permissions to the new role's defaults.
    /// </summary>
    [Authorize]
    [HttpPut("{id:guid}/role", Name = "UpdateUserRole")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> UpdateUserRole(
        Guid id,
        [FromBody] UpdateUserRoleDto dto)
    {
        var command = new UpdateUserRole.Command(id, dto.Role);
        var result = await mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Adds a permission to a User.
    /// </summary>
    [Authorize]
    [HttpPost("{id:guid}/permissions", Name = "AddUserPermission")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> AddUserPermission(
        Guid id,
        [FromBody] UserPermissionDto dto)
    {
        var command = new AddUserPermission.Command(id, dto.Permission);
        var result = await mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Removes a permission from a User.
    /// </summary>
    [Authorize]
    [HttpDelete("{id:guid}/permissions/{permission}", Name = "RemoveUserPermission")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> RemoveUserPermission(
        Guid id,
        string permission)
    {
        var command = new RemoveUserPermission.Command(id, permission);
        var result = await mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new Tenant and User together in a single transaction.
    /// Use this to onboard a new organization with its first admin user.
    /// </summary>
    [Authorize]
    [HttpPost("initiate", Name = "InitiateUserAndTenant")]
    [ProducesResponseType(typeof(InitiateUserAndTenantResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InitiateUserAndTenantResponseDto>> InitiateUserAndTenant(
        [FromBody] InitiateUserAndTenantDto dto)
    {
        var command = new InitiateUserAndTenant.Command(dto);
        var result = await mediator.Send(command);

        return CreatedAtRoute("GetUser",
            new { id = result.User.Id },
            result);
    }
}
