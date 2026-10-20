namespace FullstackTemplate.Server.Domain.Tenants.Features;

using System.ComponentModel;
using Dtos;
using MediatR;
using ModelContextProtocol.Server;
using Resources.Entrypoints.Mcp;

/// <summary>
/// The MCP tool of <see cref="GetTenantList"/>. The SDK resolves <see cref="IMediator"/> from the request services,
/// so it is not in the input schema. <c>McpDomainErrors</c> maps the domain exceptions to tool errors.
/// </summary>
[McpServerToolType]
public static class GetTenantListTool
{
    [McpServerTool(Name = "list_tenants", ReadOnly = true, UseStructuredContent = true)]
    [Description("Gets one page of the tenants that the caller can see, with the page data.")]
    public static async Task<McpPage<TenantDto>> Handle(
        IMediator mediator,
        CancellationToken cancellationToken,
        [Description("The page number. The first page is 1. Leave it out for the first page.")] int? pageNumber = null,
        [Description("The number of tenants on a page, at least 1. Leave it out for the default size. A value more than the maximum gives the maximum. The result gives the size that was used.")] int? pageSize = null,
        [Description("A QueryKit filter on id and name, for example: name @=* \"acme\"")] string? filters = null,
        [Description("A QueryKit sort order, for example: -name")] string? sortOrder = null)
    {
        var parameters = TenantParametersDto.From(pageNumber, pageSize, filters, sortOrder);
        var result = await mediator.Send(new GetTenantList.Query(parameters), cancellationToken);
        return McpPage.From(result);
    }
}
