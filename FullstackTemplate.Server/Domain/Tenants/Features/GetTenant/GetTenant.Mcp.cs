namespace FullstackTemplate.Server.Domain.Tenants.Features;

using System.ComponentModel;
using Dtos;
using MediatR;
using ModelContextProtocol.Server;
using Resources.Entrypoints.Mcp;

/// <summary>
/// The MCP tool of <see cref="GetTenant"/>. The SDK resolves <see cref="IMediator"/> from the request services,
/// so it is not in the input schema. <c>McpDomainErrors</c> maps the domain exceptions to tool errors.
/// </summary>
[McpServerToolType]
public static class GetTenantTool
{
    [McpServerTool(Name = "get_tenant", ReadOnly = true, UseStructuredContent = true)]
    [ToolAccess(ToolAccessKind.Read)]
    [Description("Gets one tenant by its ID. The caller sees only their own tenant.")]
    public static Task<TenantDto> Handle(
        IMediator mediator,
        [Description("The tenant ID, for example ten_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f.")] string id,
        CancellationToken cancellationToken)
        => mediator.Send(new GetTenant.Query(id), cancellationToken);
}
