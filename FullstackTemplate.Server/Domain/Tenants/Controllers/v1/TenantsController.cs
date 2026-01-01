namespace FullstackTemplate.Server.Domain.Tenants.Controllers.v1;

using Asp.Versioning;
using Dtos;
using Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Resources;
using Resources.Extensions;

[ApiController]
[Route("api/v{v:apiVersion}/tenants")]
[ApiVersion("1.0")]
public sealed class TenantsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Gets a single Tenant by ID.
    /// </summary>
    [Authorize]
    [HttpGet("{id:guid}", Name = "GetTenant")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDto>> GetTenant(Guid id)
    {
        var query = new GetTenant.Query(id);
        var result = await mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Gets a paginated list of Tenants.
    /// </summary>
    [Authorize]
    [HttpGet(Name = "GetTenantList")]
    [ProducesResponseType(typeof(PagedList<TenantDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedList<TenantDto>>> GetTenantList(
        [FromQuery] TenantParametersDto parameters)
    {
        var query = new GetTenantList.Query(parameters);
        var result = await mediator.Send(query);

        Response.AddPaginationHeader(result);

        return Ok(result);
    }

    /// <summary>
    /// Creates a new Tenant.
    /// </summary>
    [Authorize]
    [HttpPost(Name = "AddTenant")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TenantDto>> AddTenant(
        [FromBody] TenantForCreationDto dto)
    {
        var command = new AddTenant.Command(dto);
        var result = await mediator.Send(command);

        return CreatedAtRoute("GetTenant",
            new { id = result.Id },
            result);
    }

    /// <summary>
    /// Updates an existing Tenant.
    /// </summary>
    [Authorize]
    [HttpPut("{id:guid}", Name = "UpdateTenant")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TenantDto>> UpdateTenant(
        Guid id,
        [FromBody] TenantForUpdateDto dto)
    {
        var command = new UpdateTenant.Command(id, dto);
        var result = await mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Deletes a Tenant.
    /// </summary>
    [Authorize]
    [HttpDelete("{id:guid}", Name = "DeleteTenant")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteTenant(Guid id)
    {
        var command = new DeleteTenant.Command(id);
        await mediator.Send(command);
        return NoContent();
    }
}
