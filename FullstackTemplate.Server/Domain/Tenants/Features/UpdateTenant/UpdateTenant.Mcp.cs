namespace FullstackTemplate.Server.Domain.Tenants.Features;

using System.ComponentModel;
using Dtos;
using MediatR;
using ModelContextProtocol.Server;
using Resources.Entrypoints.Mcp;
using Users;

/// <summary>
/// The MCP tool of <see cref="UpdateTenant"/>. The SDK resolves <see cref="IMediator"/> from the request services,
/// so it is not in the input schema. <c>McpDomainErrors</c> maps the domain exceptions to tool errors.
/// </summary>
[McpServerToolType]
public static class UpdateTenantTool
{
    // A rename overwrites the name, so the tool is not additive: the MCP spec says destructive.
    [McpServerTool(Name = "update_tenant", Destructive = true, Idempotent = true, UseStructuredContent = true)]
    [ToolAccess(Permission.Names.ManageTenant)]
    [Description("Renames a tenant. The caller needs the manage_tenant permission in the tenant.")]
    public static Task<TenantDto> Handle(
        IMediator mediator,
        [Description("The tenant ID, for example ten_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f.")] string id,
        [Description("The new unique name of the tenant.")] string name,
        [Description("The version of the tenant that get_tenant gave. If the tenant changed after that, the tool gives the aborted error.")] uint version,
        CancellationToken cancellationToken)
        => mediator.Send(
            new UpdateTenant.Command(id, new TenantForUpdateDto { Name = name, Version = version }),
            cancellationToken);
}
