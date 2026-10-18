namespace FullstackTemplate.Server.Services;

/// <summary>
/// The "Tenancy" configuration section.
/// </summary>
public sealed class TenancyOptions
{
    public const string SectionName = "Tenancy";

    /// <summary>
    /// The client IDs of the machine clients that can read the rows of all tenants.
    /// A token with no "sub" claim from any other client sees no tenant data.
    /// </summary>
    public string[] AllTenantClientIds { get; init; } = [];
}
