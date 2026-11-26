namespace FullstackTemplate.Server.Domain.Users;

using System.ComponentModel.DataAnnotations.Schema;
using EmailAddresses;
using DomainEvents;
using Models;

public class User : BaseEntity, ITenantable, IDeletedWithEvent
{
    public const string IdPrefix = "usr";
    public const int FirstNameMaxLength = 200;
    public const int LastNameMaxLength = 200;
    public const int IdentifierMaxLength = 255;
    public const string IdentifierInUseMessage = "A user with this identifier already exists.";
    public const int UsernameMaxLength = 100;

    [NotSensitive] public string TenantId { get; private set; } = default!;
    [Sensitive] public string FirstName { get; private set; } = default!;
    [Sensitive] public string LastName { get; private set; } = default!;
    [Sensitive] public string Identifier { get; private set; } = default!;
    [Sensitive] public EmailAddress Email { get; private set; } = default!;
    [Sensitive] public string Username { get; private set; } = default!;

    private readonly List<UserRoleAssignment> _userRoles = [];
    public IReadOnlyCollection<UserRoleAssignment> UserRoles => _userRoles.AsReadOnly();

    private readonly List<UserPermissionOverride> _permissionOverrides = [];
    public IReadOnlyCollection<UserPermissionOverride> PermissionOverrides => _permissionOverrides.AsReadOnly();

    /// <summary>
    /// The permissions that the user holds: the union of the permissions of each role, with each permission that an
    /// override grants and without each one that an override removes. Each change of the roles or the overrides
    /// computes it again, so a read needs only these rows.
    /// </summary>
    private readonly List<UserPermission> _userPermissions = [];
    public IReadOnlyCollection<UserPermission> UserPermissions => _userPermissions.AsReadOnly();

    public string FullName => $"{FirstName} {LastName}";

    /// <summary>The save refuses to delete a user that did not queue <see cref="UserDeleted"/>.</summary>
    [NotMapped]
    internal bool DeleteQueued { get; private set; }

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
        };
        var role = UserRole.Of(userForCreation.Role);
        // Set the ID before the roles and the permissions, because each row copies the user ID.
        if (userForCreation.Id is not null)
            user.SetId(userForCreation.Id);

        user._userRoles.Add(UserRoleAssignment.Create(user, role));
        user.ComputePermissions();

        ValidateUser(user);
        user.QueueDomainEvent(new UserCreated(user.Id));

        return user;
    }

    /// <summary>Creates the user who starts a tenant. This user must be able to manage it (UM-4).</summary>
    public static User CreateFirstOfTenant(UserForCreation userForCreation)
    {
        Exceptions.ValidationException.Must(TenantManagers.CanManage(UserRole.Of(userForCreation.Role).GetPermissions()),
            "role", TenantManagers.FirstUserMessage);
        return Create(userForCreation);
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

    /// <summary>
    /// Gives the user exactly these roles. The overrides stay, so a permission that someone added or removed by hand
    /// stays added or removed.
    /// </summary>
    public User UpdateRoles(ManagerKept kept)
    {
        if (kept.UserId != Id || kept.Change is not ManagerChange.NewRoles { Roles: var newRoles })
            throw new ArgumentException("Give the proof of a role change of this user.", nameof(kept));
        if (newRoles.SetEquals(CurrentRoles()))
            return this;

        _userRoles.RemoveAll(r => !newRoles.Contains(r.Role));
        foreach (var role in newRoles.Except(CurrentRoles()).ToList())
            _userRoles.Add(UserRoleAssignment.Create(this, role));
        ComputePermissions();
        QueueDomainEvent(new UserUpdated(Id));

        return this;
    }

    /// <summary>Grants the permission whatever the roles of the user, also after a role change.</summary>
    public User AddPermission(Permission permission) => Override(permission, granted: true);

    public User AddPermissions(IEnumerable<Permission> permissions)
    {
        foreach (var permission in permissions)
        {
            AddPermission(permission);
        }

        return this;
    }

    public User RemovePermission(ManagerKept kept)
    {
        if (kept.UserId != Id || kept.Change is not ManagerChange.PermissionRemoval { Permission: var permission })
            throw new ArgumentException("Give the proof of a permission removal of this user.", nameof(kept));
        return Override(permission, granted: false);
    }

    // The last decision for a permission wins: an add replaces a removal, and a removal replaces an add
    private User Override(Permission permission, bool granted)
    {
        if (_permissionOverrides.Any(o => o.Permission == permission && o.Granted == granted))
            return this;

        _permissionOverrides.RemoveAll(o => o.Permission == permission);
        _permissionOverrides.Add(UserPermissionOverride.Create(this, permission, granted));
        ComputePermissions();
        return this;
    }

    private void ComputePermissions()
    {
        var permissions = PermissionsWithRoles(CurrentRoles());
        _userPermissions.RemoveAll(p => !permissions.Contains(p.Permission));
        foreach (var permission in permissions.Where(p => _userPermissions.All(x => x.Permission != p)))
            _userPermissions.Add(UserPermission.Create(this, permission));
    }

    /// <summary>
    /// Call it in the feature that removes the user. A handler of <see cref="UserDeleted"/> in another domain can then
    /// clear or mark a link to the user, in the same save. See <c>docs/backend/domain-events.md</c>.
    /// </summary>
    public User Delete(ManagerKept kept)
    {
        if (kept.UserId != Id || kept.Change is not ManagerChange.Removal)
            throw new ArgumentException("Give the proof of the removal of this user.", nameof(kept));
        return QueueDelete();
    }

    BaseEntity IDeletedWithEvent.Delete() => QueueDelete();

    private User QueueDelete()
    {
        QueueDomainEvent(new UserDeleted(Id));
        DeleteQueued = true;
        return this;
    }

    bool IDeletedWithEvent.DeleteQueued => DeleteQueued;

    public bool HasPermission(Permission permission)
        => _userPermissions.Any(x => x.Permission == permission);

    public bool CanManageTenant => TenantManagers.CanManage(CurrentPermissions());

    /// <summary>
    /// The actions that the caller can send for this user now. The read DTO carries them, so the SPA shows a button
    /// only for an action that <see cref="GuardAllows"/> lets through.
    /// </summary>
    /// <remarks>Load <see cref="UserPermissions"/> first, because two actions read them.</remarks>
    public IReadOnlyList<UserAction> AllowedActions(Caller caller)
    {
        var permissions = CurrentPermissions();
        return UserAction.AllowedFor(OnThisRecord(caller, permissions), Id, permissions);
    }

    /// <summary>The check that each command of <see cref="UserAction"/> makes after it loads the user.</summary>
    /// <exception cref="Exceptions.ForbiddenAccessException">The caller lacks a permission that the action needs.</exception>
    public void GuardAllows(UserAction action, Caller caller)
    {
        var permissions = CurrentPermissions();
        action.Guard(OnThisRecord(caller, permissions), Id, permissions);
    }

    internal Permission[] CurrentPermissions() => _userPermissions.Select(x => x.Permission).ToArray();

    internal UserRole[] CurrentRoles() => _userRoles.Select(x => x.Role).ToArray();

    /// <summary>
    /// The permissions that the user would hold with <paramref name="roles"/>: the union of their permissions, with
    /// each permission that an override grants and without each one that an override removes.
    /// </summary>
    /// <remarks>Load <see cref="PermissionOverrides"/> first.</remarks>
    internal IReadOnlySet<Permission> PermissionsWithRoles(IEnumerable<UserRole> roles)
        => UserRole.PermissionsOf(roles)
            .Union(_permissionOverrides.Where(o => o.Granted).Select(o => o.Permission))
            .Except(_permissionOverrides.Where(o => !o.Granted).Select(o => o.Permission))
            .ToHashSet();

    // A member holds the permissions of their own user. After a command changes them, the caller that the handler
    // loaded still has the old set, so the response would list the actions from before the change.
    private Caller OnThisRecord(Caller caller, Permission[] permissions)
        => caller.IsUser(Id) ? new Caller.Member(Id, permissions) : caller;

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
    }

    protected User() : base(IdPrefix) { } // For EF Core
}
