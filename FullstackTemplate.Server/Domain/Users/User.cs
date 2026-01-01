namespace FullstackTemplate.Server.Domain.Users;

using EmailAddresses;
using DomainEvents;
using Models;

public class User : BaseEntity, ITenantable
{
    public Guid TenantId { get; private set; }
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string Identifier { get; private set; } = default!;
    public EmailAddress Email { get; private set; } = default!;
    public string Username { get; private set; } = default!;
    public UserRole Role { get; private set; } = default!;

    private readonly List<UserPermission> _userPermissions = [];
    public IReadOnlyCollection<UserPermission> UserPermissions => _userPermissions.AsReadOnly();

    public string FullName => $"{FirstName} {LastName}";

    public static User Create(UserForCreation userForCreation)
    {
        var user = new User
        {
            TenantId = userForCreation.TenantId,
            FirstName = userForCreation.FirstName,
            LastName = userForCreation.LastName,
            Identifier = userForCreation.Identifier,
            Email = EmailAddress.Of(userForCreation.Email),
            Username = userForCreation.Username,
            Role = UserRole.Of(userForCreation.Role)
        };

        user.ResetPermissionsToDefault();

        ValidateUser(user);
        user.QueueDomainEvent(new UserCreated(user));

        return user;
    }

    public User Update(UserForUpdate userForUpdate)
    {
        FirstName = userForUpdate.FirstName;
        LastName = userForUpdate.LastName;
        Email = EmailAddress.Of(userForUpdate.Email);
        Username = userForUpdate.Username;

        ValidateUser(this);
        QueueDomainEvent(new UserUpdated(Id));

        return this;
    }

    public User UpdateFromIdp(string firstName, string lastName, string email, string username)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = EmailAddress.Of(email);
        Username = username;

        ValidateUser(this);
        QueueDomainEvent(new UserUpdated(Id));

        return this;
    }

    public User UpdateRole(UserRole newRole)
    {
        Role = newRole;
        ResetPermissionsToDefault();
        QueueDomainEvent(new UserUpdated(Id));

        return this;
    }

    public User AddPermission(Permission permission)
    {
        if (_userPermissions.Any(x => x.Permission == permission))
            return this;

        var userPermission = UserPermission.Create(this, permission);
        _userPermissions.Add(userPermission);

        return this;
    }

    public User AddPermissions(IEnumerable<Permission> permissions)
    {
        foreach (var permission in permissions)
        {
            AddPermission(permission);
        }

        return this;
    }

    public User RemovePermission(UserPermission permission)
    {
        _userPermissions.RemoveAll(x => x.Id == permission.Id);
        return this;
    }

    public User RemovePermission(Permission permission)
    {
        _userPermissions.RemoveAll(x => x.Permission == permission);
        return this;
    }

    public User ResetPermissionsToDefault()
    {
        _userPermissions.Clear();
        var defaultPermissions = Role.GetDefaultPermissions();
        AddPermissions(defaultPermissions);

        return this;
    }

    public bool HasPermission(Permission permission)
        => _userPermissions.Any(x => x.Permission == permission);

    private static void ValidateUser(User user)
    {
        Exceptions.ValidationException.ThrowWhenEmpty(user.TenantId, "Please provide a tenant.");
        Exceptions.ValidationException.ThrowWhenNullOrWhitespace(user.FirstName, "Please provide a first name.");
        Exceptions.ValidationException.ThrowWhenNullOrWhitespace(user.LastName, "Please provide a last name.");
        Exceptions.ValidationException.ThrowWhenNullOrWhitespace(user.Identifier, "Please provide an identifier.");
        Exceptions.ValidationException.ThrowWhenNullOrWhitespace(user.Username, "Please provide a username.");
        Exceptions.ValidationException.ThrowWhenNull(user.Email, "Please provide a valid email.");
        Exceptions.ValidationException.ThrowWhenNull(user.Role, "Please provide a role.");
    }

    protected User() { } // For EF Core
}
