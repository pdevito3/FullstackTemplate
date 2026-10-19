namespace FullstackTemplate.Server.Domain.Users.Mappings;

using System.Diagnostics.CodeAnalysis;
using Dtos;
using EmailAddresses;
using Models;
using Riok.Mapperly.Abstractions;

[Mapper]
public static partial class UserMapper
{
    [MapperIgnoreSource(nameof(User.CreatedBy))]
    [MapperIgnoreSource(nameof(User.LastModifiedBy))]
    [MapperIgnoreSource(nameof(User.CreatedOn))]
    [MapperIgnoreSource(nameof(User.LastModifiedOn))]
    [MapperIgnoreSource(nameof(User.IsDeleted))]
    [MapperIgnoreSource(nameof(User.DomainEvents))]
    [MapProperty(nameof(User.UserPermissions), nameof(UserDto.Permissions))]
    [MapPropertyFromSource(nameof(UserDto.FullName), Use = nameof(MapFullName))]
    public static partial UserDto ToUserDto(this User user);

    public static partial IQueryable<UserDto> ToUserDtoQueryable(this IQueryable<User> queryable);

    // The handler decides the tenant from the caller's scope. The tenant in the DTO is only a request.
    [MapperIgnoreSource(nameof(UserForCreationDto.TenantId))]
    [MapperIgnoreTarget(nameof(UserForCreation.Id))]
    public static partial UserForCreation ToUserForCreation(this UserForCreationDto dto, string tenantId);

    public static partial UserForUpdate ToUserForUpdate(this UserForUpdateDto dto);

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
