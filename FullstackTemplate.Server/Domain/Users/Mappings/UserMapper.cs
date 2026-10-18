namespace FullstackTemplate.Server.Domain.Users.Mappings;

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
    public static partial UserForCreation ToUserForCreation(this UserForCreationDto dto, Guid tenantId);

    public static partial UserForUpdate ToUserForUpdate(this UserForUpdateDto dto);

    private static string MapEmail(EmailAddress email) => email.Value;
    // The projection inlines this expression, so EF can filter and sort on FullName. User.FullName is not a column.
    private static string MapFullName(User user) => user.FirstName + " " + user.LastName;
    private static string MapRole(UserRole role) => role.Value;
    private static IReadOnlyList<string> MapUserPermissionsToPermissions(IReadOnlyCollection<UserPermission> permissions)
        => permissions.Select(p => p.Permission.Value).ToList();
}
