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
    public static partial UserDto ToUserDto(this User user);

    public static partial IQueryable<UserDto> ToUserDtoQueryable(this IQueryable<User> queryable);

    public static partial UserForCreation ToUserForCreation(this UserForCreationDto dto);

    public static partial UserForUpdate ToUserForUpdate(this UserForUpdateDto dto);

    private static string MapEmail(EmailAddress email) => email.Value;
}
