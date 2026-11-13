namespace FullstackTemplate.Server.Resources.Entrypoints.Mcp;

using ModelContextProtocol.Server;

/// <summary>
/// Makes each MCP tool declare its risk to the client. A client uses these hints, for example to ask the user before
/// a destructive call. MCP takes a hint that is not set as the worst case: destructive, and open to the world.
/// </summary>
public static class McpToolHints
{
    /// <summary>
    /// Stops startup when a tool sets neither <c>ReadOnly = true</c> nor <c>Destructive</c>, or does not set
    /// <c>OpenWorld</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">A tool has a hint that is not set. The message names each one.</exception>
    public static void Validate(IEnumerable<McpServerTool> tools)
    {
        var problems = tools.SelectMany(ProblemsOf).ToList();
        if (problems.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, problems));
    }

    private static IEnumerable<string> ProblemsOf(McpServerTool tool)
    {
        var name = tool.ProtocolTool.Name;
        var annotations = tool.ProtocolTool.Annotations;
        if (annotations?.ReadOnlyHint is not true && annotations?.DestructiveHint is null)
            yield return $"The MCP tool {name} does not declare if it changes data. "
                + "Set ReadOnly = true for a read, or set Destructive for a write.";
        if (annotations?.OpenWorldHint is null)
            yield return $"The MCP tool {name} does not declare if it touches systems outside the app. "
                + "Set OpenWorld = false for a tool that uses only the app database.";
    }
}
