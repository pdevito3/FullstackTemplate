namespace FullstackTemplate.Server.Examples.Workshops.Features;

using System.ComponentModel;
using Dtos;
using MediatR;
using ModelContextProtocol.Server;
using Resources.Entrypoints.Mcp;

/// <summary>
/// The MCP tool of <see cref="GetWorkshopList"/>. The SDK resolves <see cref="IMediator"/> from the request services,
/// so it is not in the input schema. <c>McpDomainErrors</c> maps the domain exceptions to tool errors.
/// </summary>
[McpServerToolType]
public static class GetWorkshopListTool
{
    [McpServerTool(Name = "list_workshops", ReadOnly = true, UseStructuredContent = true)]
    [ToolAccess(ToolAccessKind.Read)]
    [Description("Gets one page of the workshops of the caller's tenant, with the page data.")]
    public static async Task<McpPage<WorkshopDto>> Handle(
        IMediator mediator,
        CancellationToken cancellationToken,
        [Description("A text that the title contains, with no regard to case.")] string? search = null,
        [Description("The page number. The first page is 1. Leave it out for the first page.")] int? pageNumber = null,
        [Description("The number of workshops on a page, at least 1. Leave it out for the default size. A value more than the maximum gives the maximum. The result gives the size that was used.")] int? pageSize = null,
        [Description("A QueryKit filter on the fields of a workshop, for example: status == \"Scheduled\"")] string? filters = null,
        [Description("A QueryKit sort order, for example: -startsAt")] string? sortOrder = null)
    {
        var parameters = WorkshopParametersDto.From(pageNumber, pageSize, filters, sortOrder, search);
        var result = await mediator.Send(new GetWorkshopList.Query(parameters), cancellationToken);
        return McpPage.From(result);
    }
}
