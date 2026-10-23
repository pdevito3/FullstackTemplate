namespace FullstackTemplate.Server.Domain.Tenants.Features;

using System.ComponentModel;
using Dtos;
using MediatR;
using ModelContextProtocol.Server;
using Resources.Entrypoints.Mcp;

/// <summary>
/// The MCP tool of <see cref="AddTenant"/>. The SDK resolves <see cref="IMediator"/> from the request services,
/// so it is not in the input schema. <c>McpDomainErrors</c> maps the domain exceptions to tool errors.
/// </summary>
[McpServerToolType]
public static class AddTenantTool
{
    [McpServerTool(Name = "add_tenant", Destructive = false, UseStructuredContent = true)]
    [ToolAccess(ToolAccessKind.ManageAllTenants)]
    [Description("Adds a tenant. Only a trusted client that can manage all tenants can do this.")]
    public static Task<TenantDto> Handle(
        IMediator mediator,
        [Description("The unique name of the tenant.")] string name,
        CancellationToken cancellationToken)
        => mediator.Send(new AddTenant.Command(new TenantForCreationDto { Name = name }), cancellationToken);
}
