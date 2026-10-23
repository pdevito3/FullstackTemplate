namespace FullstackTemplate.Server.Services;

/// <summary>
/// The "Tenancy" configuration section.
/// </summary>
public sealed class TenancyOptions
{
    public const string SectionName = "Tenancy";

    /// <summary>
    /// The client IDs of the machine clients that can read the rows of all tenants. These clients cannot
    /// add, change, or delete anything. Each token of a listed client matches, whatever its "sub" claim.
    /// </summary>
    public string[] ReadAllTenantsClientIds { get; init; } = [];

    /// <summary>
    /// The client IDs of the machine clients that can manage all tenants: add a tenant, start a tenant for
    /// another user, and do the work of a tenant admin in any tenant. These clients also read the rows of all tenants.
    /// </summary>
    public string[] ManageAllTenantsClientIds { get; init; } = [];
}
