namespace FullstackTemplate.Server.Domain.Users.Dtos;

using Resources;

public sealed record UpdateUserRolesDto
{
    /// <summary>
    /// Every role that the user gets. The user holds the union of their permissions. A permission that someone added to
    /// or removed from the user by hand stays added or removed.
    /// </summary>
    [RequiredField]
    [NamesOf<UserRole>]
    public IReadOnlyList<string> Roles { get; init; } = default!;
}
