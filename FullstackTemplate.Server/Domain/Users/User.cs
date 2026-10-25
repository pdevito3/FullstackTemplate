namespace FullstackTemplate.Server.Domain.Users;

using EmailAddresses;
using DomainEvents;
using Models;

public class User : BaseEntity, ITenantable
{
    public const string IdPrefix = "usr";
    public const int FirstNameMaxLength = 200;
    public const int LastNameMaxLength = 200;
    public const int IdentifierMaxLength = 255;
    public const int UsernameMaxLength = 100;

    [NotSensitive] public string TenantId { get; private set; } = default!;
    [Sensitive] public string FirstName { get; private set; } = default!;
    [Sensitive] public string LastName { get; private set; } = default!;
    [Sensitive] public string Identifier { get; private set; } = default!;
    [Sensitive] public EmailAddress Email { get; private set; } = default!;
    [Sensitive] public string Username { get; private set; } = default!;
    [NotSensitive] public UserRole Role { get; private set; } = default!;

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
        // Set the ID before the permissions, because each permission copies the user ID.
        if (userForCreation.Id is not null)
            user.SetId(userForCreation.Id);

        user.ResetPermissionsToDefault();

        ValidateUser(user);
        user.QueueDomainEvent(new UserCreated(user.Id));

        return user;
    }

    public User Update(UserForUpdate userForUpdate)
    {
        GuardVersion(userForUpdate.Version);
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
        Exceptions.ValidationException.ThrowWhenNullOrWhitespace(user.TenantId, "tenantId", "Please provide a tenant.");
        Exceptions.ValidationException.Must(EntityId.IsValid(user.TenantId, Tenants.Tenant.IdPrefix), "tenantId", "Please provide a valid tenant ID.");
        Exceptions.ValidationException.ThrowWhenNullOrWhitespace(user.FirstName, "firstName", "Please provide a first name.");
        Exceptions.ValidationException.ThrowWhenLongerThan(user.FirstName, FirstNameMaxLength, "firstName",
            $"A first name can have no more than {FirstNameMaxLength} characters.");
        Exceptions.ValidationException.ThrowWhenHasCharacterPostgresCannotStore(user.FirstName, "firstName",
            "A first name has a character that is not allowed.");
        Exceptions.ValidationException.ThrowWhenNullOrWhitespace(user.LastName, "lastName", "Please provide a last name.");
        Exceptions.ValidationException.ThrowWhenLongerThan(user.LastName, LastNameMaxLength, "lastName",
            $"A last name can have no more than {LastNameMaxLength} characters.");
        Exceptions.ValidationException.ThrowWhenHasCharacterPostgresCannotStore(user.LastName, "lastName",
            "A last name has a character that is not allowed.");
        Exceptions.ValidationException.ThrowWhenNullOrWhitespace(user.Identifier, "identifier", "Please provide an identifier.");
        Exceptions.ValidationException.ThrowWhenLongerThan(user.Identifier, IdentifierMaxLength, "identifier",
            $"An identifier can have no more than {IdentifierMaxLength} characters.");
        Exceptions.ValidationException.ThrowWhenHasCharacterPostgresCannotStore(user.Identifier, "identifier",
            "An identifier has a character that is not allowed.");
        Exceptions.ValidationException.ThrowWhenNullOrWhitespace(user.Username, "username", "Please provide a username.");
        Exceptions.ValidationException.ThrowWhenLongerThan(user.Username, UsernameMaxLength, "username",
            $"A username can have no more than {UsernameMaxLength} characters.");
        Exceptions.ValidationException.ThrowWhenHasCharacterPostgresCannotStore(user.Username, "username",
            "A username has a character that is not allowed.");
        Exceptions.ValidationException.ThrowWhenNull(user.Role, "role", "Please provide a role.");
    }

    protected User() : base(IdPrefix) { } // For EF Core
}
