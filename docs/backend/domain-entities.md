# Domain entities

A domain entity holds its data and its business rules. Code outside the entity cannot change its state directly. The examples come from `Domain/Users/User.cs`.

## Rules

1. Give each property a private setter.
2. Create the entity with a static `Create` method that takes a creation model. Do not use a public constructor.
3. Add one public method for each change, for example `Update`, `UpdateRole`, or `AddPermission`. Each method returns `this`.
4. Validate in the entity. Throw a `ValidationException` when a rule fails. Give it the name of the request field in camelCase, for example `"firstName"`. This name is the key in the `errors` of the problem.
5. Queue a domain event in each method that changes the state. See [domain-events.md](domain-events.md).
6. Keep collections private. Expose an `IReadOnlyCollection<T>`, and change it only with entity methods.
7. Use a value object for a domain concept, such as an email or a role. Do not use a bare `string`.

## IDs

Each entity has a `public const string IdPrefix`. `BaseEntity` makes the ID as `{prefix}_{uuidv7}`, for example `usr_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f`. IDs are strings, not `Guid` values. The build gives an error when an entity breaks a prefix rule. See [backend.md](../backend.md#main-concepts).

The constructor for EF Core is `protected` and calls `base(IdPrefix)`.

## Example

```csharp
public class User : BaseEntity, ITenantable
{
    public const string IdPrefix = "usr";

    public string FirstName { get; private set; } = default!;
    public EmailAddress Email { get; private set; } = default!;
    public UserRole Role { get; private set; } = default!;

    private readonly List<UserPermission> _userPermissions = [];
    public IReadOnlyCollection<UserPermission> UserPermissions => _userPermissions.AsReadOnly();

    public static User Create(UserForCreation userForCreation)
    {
        var user = new User
        {
            FirstName = userForCreation.FirstName,
            Email = EmailAddress.Of(userForCreation.Email),
            Role = UserRole.Of(userForCreation.Role)
        };
        // Set the ID before the permissions, because each permission copies the user ID.
        if (userForCreation.Id is not null)
            user.SetId(userForCreation.Id);

        user.ResetPermissionsToDefault();

        ValidateUser(user);
        user.QueueDomainEvent(new UserCreated(user));

        return user;
    }

    public User UpdateRole(UserRole newRole)
    {
        Role = newRole;
        ResetPermissionsToDefault();
        QueueDomainEvent(new UserUpdated(Id));

        return this;
    }

    private static void ValidateUser(User user)
    {
        ValidationException.ThrowWhenNullOrWhitespace(user.FirstName, "firstName", "Please provide a first name.");
        ValidationException.Must(EntityId.IsValid(user.TenantId, Tenant.IdPrefix), "tenantId", "Please provide a valid tenant ID.");
    }

    protected User() : base(IdPrefix) { } // For EF Core
}
```

The creation model has an `Id` only for tests. If the model has an `Id`, call `SetId` before you add child rows, because each child copies the parent ID. See [dtos-and-mappings.md](dtos-and-mappings.md#models).

## Guards

When a rule blocks a change in some states, put the rule in a private guard method. Call the guard at the start of each method that it protects.

```csharp
private void GuardIfInFinalState(string field, string subject)
{
    if (Status.IsFinalState())
        throw new ValidationException(field, $"This order is in a final state. {subject} can not be modified.");
}
```

## Value objects

A value object derives from `ValueObject`. It validates its value in the constructor and has a static `Of` method. See `Domain/EmailAddresses/EmailAddress.cs`. A FluentValidation rule in a value object names the request field with `OverridePropertyName`, for example `"email"`, so that the 422 has the key in `errors`.

When a value has fixed options, back the value object with a private SmartEnum. Put the behavior for each option on the SmartEnum. `UserRole` is the example:

```csharp
public partial class UserRole : ValueObject
{
    private const string RequestField = "role";
    private UserRoleEnum _role = null!;

    public string Value
    {
        get => _role.Name;
        private set
        {
            if (!UserRoleEnum.TryFromName(value, true, out var parsed))
                throw new ValidationException(RequestField, $"Invalid role: {value}");
            _role = parsed;
        }
    }

    public Permission[] GetDefaultPermissions() => _role.GetDefaultPermissions();
    public bool IsAdmin() => _role == UserRoleEnum.Admin;

    public static UserRole Of(string value) => new(value);
    public static UserRole Admin() => new(UserRoleEnum.Admin.Name);

    private abstract class UserRoleEnum(string name, int value) : SmartEnum<UserRoleEnum>(name, value)
    {
        public static readonly UserRoleEnum Admin = new AdminType();
        public abstract Permission[] GetDefaultPermissions();

        private class AdminType() : UserRoleEnum("Admin", 0)
        {
            public override Permission[] GetDefaultPermissions() => [Permission.DoSomethingSpecial()];
        }
    }
}
```

To map a value object to a column, see [entity-configurations.md](entity-configurations.md#value-objects).

## Do not

- Do not give an entity public setters.
- Do not change an entity from a handler or a service. Add a method to the entity.
- Do not expose a `List<T>` that callers can change.
