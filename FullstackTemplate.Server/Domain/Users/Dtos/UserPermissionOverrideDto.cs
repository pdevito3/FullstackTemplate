namespace FullstackTemplate.Server.Domain.Users.Dtos;

/// <summary>A permission that someone added to the user or removed from the user by hand, on top of their roles.</summary>
public sealed record UserPermissionOverrideDto
{
    [NamesOf<Permission>]
    public string Permission { get; init; } = default!;
    /// <summary>True when the user holds the permission whatever their roles, false when they never hold it.</summary>
    public bool Granted { get; init; }
}
