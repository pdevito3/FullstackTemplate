namespace FullstackTemplate.Server.Examples.Workshops.Features;

using System.ComponentModel;
using Domain.Users;
using Dtos;
using MediatR;
using ModelContextProtocol.Server;
using Resources.Entrypoints.Mcp;

/// <summary>
/// The MCP tool of <see cref="CancelWorkshop"/>. The SDK resolves <see cref="IMediator"/> from the request services,
/// so it is not in the input schema. <c>McpDomainErrors</c> maps the domain exceptions to tool errors.
/// </summary>
[McpServerToolType]
public static class CancelWorkshopTool
{
    [McpServerTool(Name = "cancel_workshop", Destructive = true, Idempotent = true, UseStructuredContent = true)]
    [ToolAccess(Permission.Names.ManageWorkshops)]
    [Description("Cancels a draft or scheduled workshop. The caller needs the manage_workshops permission in the tenant. Only the organizer of the workshop or a caller with manage_tenant can cancel it, so an agent needs manage_tenant.")]
    public static Task<WorkshopDto> Handle(
        IMediator mediator,
        [Description("The workshop ID, for example wks_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f.")] string id,
        CancellationToken cancellationToken)
        => mediator.Send(new CancelWorkshop.Command(id), cancellationToken);
}
