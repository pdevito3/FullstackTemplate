namespace FullstackTemplate.Server.Domain.Users;

using Ardalis.SmartEnum;
using Exceptions;

public class Permission : ValueObject
{
    private PermissionEnum _permission = null!;

    public string Value
    {
        get => _permission.Name;
        private set
        {
            if (!PermissionEnum.TryFromName(value, true, out var parsed))
                throw new ValidationException(nameof(Permission), $"Invalid permission: {value}");

            _permission = parsed;
        }
    }

    public Permission(string value)
    {
        Value = value;
    }

    public static Permission Of(string value) => new(value);
    public static implicit operator string(Permission value) => value.Value;
    public static List<string> ListNames() => PermissionEnum.List.Select(x => x.Name).ToList();
    public static List<Permission> GetAll()
        => PermissionEnum.List.Select(x => new Permission(x.Name)).ToList();

    public static Permission DoSomethingSpecial() => new(PermissionEnum.DoSomethingSpecial.Name);

    protected Permission() { } // EF Core

    private class PermissionEnum(string name, int value) : SmartEnum<PermissionEnum>(name, value)
    {
        public static readonly PermissionEnum DoSomethingSpecial = new("do_something_special", 0);
    }
}
