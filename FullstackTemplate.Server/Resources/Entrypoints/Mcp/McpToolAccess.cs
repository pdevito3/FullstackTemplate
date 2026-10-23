namespace FullstackTemplate.Server.Resources.Entrypoints.Mcp;

using Domain.Users;
using Exceptions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

/// <summary>
/// Shows each caller only the MCP tools it can use, and denies a call to any other tool. Both filters read the
/// <see cref="ToolAccessAttribute"/> of the tool and ask <see cref="Caller"/>, so the list and the call agree.
/// </summary>
public static class McpToolAccess
{
    /// <summary>Hides each tool that the caller cannot use. It loads the caller once for the list.</summary>
    public static McpRequestHandler<ListToolsRequestParams, ListToolsResult> ListFilter(
        McpRequestHandler<ListToolsRequestParams, ListToolsResult> next) =>
        async (context, cancellationToken) =>
        {
            var result = await next(context, cancellationToken);
            var caller = await GetCallerAsync(context, cancellationToken);
            var tools = context.Server.ServerOptions.ToolCollection;

            result.Tools =
            [
                .. result.Tools.Where(listed => tools is not null
                    && tools.TryGetPrimitive(listed.Name, out var tool)
                    && RequirementOf(tool) is { } requirement
                    && requirement.Allows(caller)),
            ];
            return result;
        };

    /// <summary>
    /// Denies a call that the caller cannot make. Add it after <see cref="McpDomainErrors.Filter"/>, so that it runs
    /// inside that filter and its <see cref="ForbiddenAccessException"/> becomes a <c>permission_denied</c> tool error.
    /// A call to an unknown tool has no matched tool, so it goes on, and the SDK answers it.
    /// </summary>
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> CallFilter(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
        async (context, cancellationToken) =>
        {
            if (context.MatchedPrimitive is McpServerTool tool)
            {
                var requirement = RequirementOf(tool)
                    ?? throw new ForbiddenAccessException($"The tool {tool.ProtocolTool.Name} declares no access rule.");
                requirement.Demand(await GetCallerAsync(context, cancellationToken));
            }

            return await next(context, cancellationToken);
        };

    /// <summary>
    /// Stops startup when a tool has no <see cref="ToolAccessAttribute"/>, a kind that is not defined, or a permission
    /// that does not exist.
    /// </summary>
    /// <exception cref="InvalidOperationException">A tool has no valid declaration. The message names each one.</exception>
    public static void Validate(IEnumerable<McpServerTool> tools)
    {
        var problems = tools
            .Where(tool => RequirementOf(tool) is null)
            .Select(tool => $"The MCP tool {tool.ProtocolTool.Name} has no valid [ToolAccess] attribute. "
                + "Declare a ToolAccessKind or a Permission.Names constant.")
            .ToList();
        if (problems.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// The one parse of the <see cref="ToolAccessAttribute"/> of a tool. Null when the attribute is missing, its kind
    /// is not defined, or its permission does not exist.
    /// </summary>
    private static ToolRequirement? RequirementOf(McpServerTool tool)
        => tool.Metadata.OfType<ToolAccessAttribute>().SingleOrDefault() switch
        {
            { PermissionName: { } permission } when Permission.ListNames().Contains(permission, StringComparer.Ordinal)
                => new ToolRequirement.Needs(Permission.Of(permission)),
            { PermissionName: null, Kind: ToolAccessKind.Read } => new ToolRequirement.Read(),
            { PermissionName: null, Kind: ToolAccessKind.ManageAllTenants } => new ToolRequirement.ManageAllTenants(),
            _ => null,
        };

    private static Task<Caller> GetCallerAsync(MessageContext context, CancellationToken cancellationToken)
    {
        var services = context.Services
            ?? throw new InvalidOperationException("The MCP request has no services, so it cannot load the caller.");
        return services.GetRequiredService<IPermissionGuard>().GetCallerAsync(cancellationToken);
    }

    /// <summary>The parsed declaration. <see cref="Allows"/> and <see cref="Demand"/> ask the same member of Caller.</summary>
    private abstract record ToolRequirement
    {
        public abstract bool Allows(Caller caller);

        /// <exception cref="ForbiddenAccessException">The caller cannot use the tool. Caller gives the text.</exception>
        public abstract void Demand(Caller caller);

        public sealed record Read : ToolRequirement
        {
            public override bool Allows(Caller caller) => caller.IsRecognized;
            public override void Demand(Caller caller) => caller.RequireRecognized();
        }

        public sealed record ManageAllTenants : ToolRequirement
        {
            public override bool Allows(Caller caller) => caller.ManagesAllTenants;
            public override void Demand(Caller caller) => caller.RequireManagesAllTenants();
        }

        public sealed record Needs(Permission Permission) : ToolRequirement
        {
            public override bool Allows(Caller caller) => caller.Has(Permission);
            public override void Demand(Caller caller) => caller.Require(Permission);
        }
    }
}
