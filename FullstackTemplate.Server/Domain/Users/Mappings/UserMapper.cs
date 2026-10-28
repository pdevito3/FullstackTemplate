namespace FullstackTemplate.Server.Domain.Users.Mappings;

using System.Diagnostics.CodeAnalysis;
using Dtos;
using EmailAddresses;
using Models;
using Riok.Mapperly.Abstractions;

[Mapper]
public static partial class UserMapper
{
    /// <summary>The DTO with the actions that <paramref name="caller"/> can send for the user.</summary>
    public static UserDto ToUserDto(this User user, Caller caller)
        => user.ToUserDtoWithoutActions() with { AllowedActions = NamesOf(user.AllowedActions(caller)) };

    /// <summary>
    /// Gives each row of a list the actions that <paramref name="caller"/> can send for it. The list query projects
    /// to the DTO, so the rule reads the ID and the permissions of the DTO.
    /// </summary>
    public static void AddAllowedActions(this List<UserDto> users, Caller caller)
    {
        for (var i = 0; i < users.Count; i++)
        {
            var permissions = users[i].Permissions.Select(p => Permission.Of(p)).ToArray();
            users[i] = users[i] with { AllowedActions = NamesOf(UserAction.AllowedFor(caller, users[i].Id, permissions)) };
        }
    }

    // It takes the configuration of ToUserDtoWithoutActions. The list handler adds the actions after the page loads.
    public static partial IQueryable<UserDto> ToUserDtoQueryable(this IQueryable<User> queryable);

    // The handler decides the tenant from the caller's scope. The tenant in the DTO is only a request.
    [MapperIgnoreSource(nameof(UserForCreationDto.TenantId))]
    [MapperIgnoreTarget(nameof(UserForCreation.Id))]
    public static partial UserForCreation ToUserForCreation(this UserForCreationDto dto, string tenantId);

    public static partial UserForUpdate ToUserForUpdate(this UserForUpdateDto dto);

    [MapperIgnoreSource(nameof(User.LastModifiedOn))]
    [MapperIgnoreSource(nameof(User.IsDeleted))]
    [MapperIgnoreSource(nameof(User.DomainEvents))]
    [MapperIgnoreTarget(nameof(UserDto.AllowedActions))]
    [MapProperty(nameof(User.UserPermissions), nameof(UserDto.Permissions))]
    [MapPropertyFromSource(nameof(UserDto.FullName), Use = nameof(MapFullName))]
    private static partial UserDto ToUserDtoWithoutActions(this User user);

    private static List<string> NamesOf(IEnumerable<UserAction> actions) => actions.Select(a => a.Value).ToList();
    private static string MapEmail(EmailAddress email) => email.Value;
    // The projection inlines this expression, so EF can filter and sort on FullName. User.FullName is not a column.
    private static string MapFullName(User user) => user.FirstName + " " + user.LastName;
    private static string MapRole(UserRole role) => role.Value;
    // Mapperly uses this method only when its return type is the type of UserDto.Permissions.
    // With List<string>, Mapperly ignores it and maps each permission with ToString().
    [SuppressMessage("Performance", "CA1859", Justification = "Mapperly matches on the exact target type.")]
    private static IReadOnlyList<string> MapUserPermissionsToPermissions(IReadOnlyCollection<UserPermission> permissions)
        => permissions.Select(p => p.Permission.Value).ToList();
}
