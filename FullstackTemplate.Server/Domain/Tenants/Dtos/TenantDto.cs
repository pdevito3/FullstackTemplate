namespace FullstackTemplate.Server.Domain.Tenants.Dtos;

public sealed record TenantDto
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    /// <summary>The actions that the caller can send for this tenant now. A button in the SPA reads this list.</summary>
    [NamesOf<TenantAction>]
    public IReadOnlyList<string> AllowedActions { get; init; } = [];
    public uint Version { get; init; }
}
