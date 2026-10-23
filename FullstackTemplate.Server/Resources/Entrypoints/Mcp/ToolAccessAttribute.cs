namespace FullstackTemplate.Server.Resources.Entrypoints.Mcp;

using Domain.Users;

/// <summary>
/// What a caller needs to see and call an MCP tool. Put exactly one on each <c>[McpServerTool]</c> method, next to it.
/// <see cref="McpToolAccess"/> hides the tool from a caller that does not meet it and denies the call. The handler
/// still makes its own check, so this attribute only decides what the caller sees first.
/// </summary>
/// <remarks>
/// It implements no ASP.NET Core authorization interface, so the authorization filters of the SDK ignore it, and it
/// is not <c>[McpMeta]</c>, so it does not go on the wire. A tool without it stops startup.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ToolAccessAttribute : Attribute
{
    /// <summary>A tool that needs no permission (<see cref="ToolAccessKind.Read"/>) or that needs a capability.</summary>
    public ToolAccessAttribute(ToolAccessKind kind) => Kind = kind;

    /// <summary>A tool that needs the permission with the name <paramref name="permissionName"/>.</summary>
    /// <param name="permissionName">A constant from <see cref="Permission.Names"/>. Startup checks the name.</param>
    public ToolAccessAttribute(string permissionName) => PermissionName = permissionName;

    /// <summary>The kind of the need. Null when the tool needs a permission.</summary>
    public ToolAccessKind? Kind { get; }

    /// <summary>The name of the permission that the tool needs. Null when the tool needs no permission.</summary>
    public string? PermissionName { get; }
}

public enum ToolAccessKind
{
    /// <summary>
    /// A read. Each recognized caller can call it, and the tenant filter decides the rows. An unknown client cannot.
    /// </summary>
    Read,

    /// <summary>Only a caller that manages all tenants can call it, for example <c>add_tenant</c>.</summary>
    ManageAllTenants,
}
