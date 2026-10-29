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
8. In a method that applies the fields of an edit form, call `GuardVersion` first. See [Versions](#versions).
9. Give each property `[Sensitive]` or `[NotSensitive]`, except a flag, an enum, a `Guid`, an integer, or a foreign key. The audit log shows a sensitive value only to a reader with `read_audit_log_sensitive`. See [audit-log.md](audit-log.md#rules).
10. Give each text value that a request sets a max length constant, for example `User.FirstNameMaxLength` or `EmailAddress.MaxLength`. Refuse a longer value with `ValidationException.ThrowWhenLongerThan`, and put the limit in its message. It counts characters as Postgres does. Do not compare `.Length`, because it counts an emoji as two. Use the same constant in `HasMaxLength`. Without the check, a long value fails the save in Postgres, and the API gives 500, not 422. Also refuse a value with a character that Postgres cannot store, U+0000 or a lone surrogate, with `ValidationException.ThrowWhenHasCharacterPostgresCannotStore`. Its message says that the value has a character that is not allowed. Other control characters, such as a tab or a line feed, are allowed, because Postgres stores them. `StringColumnLengthTests` fails on each text column with a max length that is not a key or a foreign key, also one stored through `HasConversion`, until you list it as a caller column with its domain write, a fixed-option column, or a server column. It also fails when the domain write of a caller column takes U+0000 or a lone surrogate.

## IDs

Each entity has a `public const string IdPrefix`. `BaseEntity` makes the ID as `{prefix}_{uuidv7}`, for example `usr_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f`. IDs are strings, not `Guid` values. The build gives an error when an entity breaks a prefix rule. See [backend.md](../backend.md#main-concepts).

The constructor for EF Core is `protected` and calls `base(IdPrefix)`.

## Example

```csharp
public class User : BaseEntity, ITenantable
{
    public const string IdPrefix = "usr";
    public const int FirstNameMaxLength = 200;

    [Sensitive] public string FirstName { get; private set; } = default!;
    [Sensitive] public EmailAddress Email { get; private set; } = default!;
    [NotSensitive] public UserRole Role { get; private set; } = default!;

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
        user.QueueDomainEvent(new UserCreated(user.Id));

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
        ValidationException.ThrowWhenLongerThan(user.FirstName, FirstNameMaxLength, "firstName",
            $"A first name can have no more than {FirstNameMaxLength} characters.");
        ValidationException.ThrowWhenHasCharacterPostgresCannotStore(user.FirstName, "firstName",
            "A first name has a character that is not allowed.");
        ValidationException.Must(EntityId.IsValid(user.TenantId, Tenant.IdPrefix), "tenantId", "Please provide a valid tenant ID.");
    }

    protected User() : base(IdPrefix) { } // For EF Core
}
```

The creation model has an `Id` only for tests. If the model has an `Id`, call `SetId` before you add child rows, because each child copies the parent ID. See [dtos-and-mappings.md](dtos-and-mappings.md#models).

## Versions

`BaseEntity.Version` is the version of the row that the entity came from. A method that replaces fields from an edit form gets the version that the caller edited in its update model. It calls `GuardVersion` before it changes a value. When the versions are not the same, `GuardVersion` throws a `StaleVersionException`, which gives 409. Thus a stale form cannot overwrite the change of another user.

```csharp
public Tenant Update(TenantForUpdate tenantForUpdate)
{
    GuardVersion(tenantForUpdate.Version);
    Name = tenantForUpdate.Name;

    ValidateTenant(this);
    QueueDomainEvent(new TenantUpdated(Id));

    return this;
}
```

A method for one intent, such as `UpdateRole` or `AddPermission`, gets no version. The context still checks the loaded version when it saves, and the command runs again when it loses. See [concurrency.md](concurrency.md#run-again).

## Guards

When a rule blocks a change in some states, put the rule in a private guard method. Call the guard at the start of each method that it protects.

```csharp
private void GuardIfInFinalState(string field, string subject)
{
    if (Status.IsFinalState())
        throw new ValidationException(field, $"This order is in a final state. {subject} can not be modified.");
}
```

When the rule needs facts from other aggregates, or a second caller needs the same answer, use the decision shape in [features-and-cqrs.md](features-and-cqrs.md#one-rule-set-for-two-callers).

<!-- example:start -->
In the workshop example, `Workshop.Update` checks the version first, then refuses a status that cannot change: [Workshop.cs:47](../../FullstackTemplate.Server/Examples/Workshops/Workshop.cs#L47).
<!-- example:end -->

### Allowed actions

When the SPA shows a button for an action on a record, the entity gives two methods that read one table:

- `AllowedActions(caller)` gives the actions that the caller can send now. The read DTO carries them (rule 11 of [dtos-and-mappings.md](dtos-and-mappings.md)).
- `GuardAllows(action, caller)` throws the refusal of the action. Each command of the action calls it after the load.

The table is an action value object over a private SmartEnum, as `Domain/Users/UserAction.cs` is. Each option holds what the action needs, for example its permissions. The list keeps each action that the guard lets through, so the list and the guard cannot disagree. A list query that projects to the DTO has no entity, so `UserMapper.AddAllowedActions` reads the ID and the permissions of the DTO. It must call the same `UserAction.AllowedFor`, so that a row of the list and the record give the same actions. Load each part of the entity that the table reads, such as `UserPermissions`, before you map the DTO, or the list reads an empty collection. A check that reads the request, such as the permission that a request grants, stays in the command.

On their own record, a member holds the permissions of the user. `User` makes the caller again from the current permissions, so the response of a command that changes them lists the actions after the change. A delegated agent keeps the permissions that it loaded, so after it changes the permissions of its own user, the response can list an action that the next request refuses with 403.

```csharp
public IReadOnlyList<UserAction> AllowedActions(Caller caller)
{
    var permissions = CurrentPermissions();
    return UserAction.AllowedFor(OnThisRecord(caller, permissions), Id, permissions);
}

public void GuardAllows(UserAction action, Caller caller)
{
    var permissions = CurrentPermissions();
    action.Guard(OnThisRecord(caller, permissions), Id, permissions);
}
```

The command checks the part of the rule that needs only the ID before the load, so a caller without the permission gets 403 for any ID. Then it checks the full rule after the load:

```csharp
var caller = await permissionGuard.GetCallerAsync(cancellationToken);
UserAction.Delete().GuardBeforeLoad(caller, request.Id);

var user = await dbContext.Users.Include(u => u.UserPermissions).GetById(request.Id, cancellationToken);
user.GuardAllows(UserAction.Delete(), caller);
```

`UserActionTests` has the tests of the table. One test gives each action the refusal text of each case. Another checks, for each caller in a small table, that an action is in `AllowedActions` only when `GuardAllows` does not throw.

<!-- example:start -->
In the workshop example, each action needs only a permission. The command calls `WorkshopAction.GuardCaller` before the load ([WorkshopAction.cs:34](../../FullstackTemplate.Server/Examples/Workshops/WorkshopAction.cs#L34)), and the entity refuses a status that does not permit the action. `Workshop.AllowedActions` ([Workshop.cs:75](../../FullstackTemplate.Server/Examples/Workshops/Workshop.cs#L75)) and `WorkshopMapper.AddAllowedActions` ([WorkshopMapper.cs:19](../../FullstackTemplate.Server/Examples/Workshops/Mappings/WorkshopMapper.cs#L19)) call the same `WorkshopAction.AllowedFor` ([WorkshopAction.cs:37](../../FullstackTemplate.Server/Examples/Workshops/WorkshopAction.cs#L37)).
<!-- example:end -->

## Value objects

A value object derives from `ValueObject`. It validates its value in the constructor and has a static `Of` method. See `Domain/EmailAddresses/EmailAddress.cs`. A FluentValidation rule in a value object names the request field with `OverridePropertyName`, for example `"email"`, so that the 422 has the key in `errors`.

When a value has fixed options, back the value object with a private SmartEnum. Put the behavior for each option on the SmartEnum. Override `ToString` to give `Value`, so that a failed test assertion and a log show the option, not the type name. Do not give `ToString` to a value object that holds personal data, such as `EmailAddress`, because a log of the object then shows the data. Implement `IFixedNames`, so that a DTO field can list the names in the OpenAPI document (see rule 9 of [dtos-and-mappings.md](dtos-and-mappings.md)). `UserRole` is the example:

```csharp
public partial class UserRole : ValueObject, IFixedNames
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

    public override string ToString() => Value;

    public static List<string> ListNames() => UserRoleEnum.List.Select(x => x.Name).ToList();

    public static UserRole Of(string value) => new(value);
    public static UserRole Admin() => new(UserRoleEnum.Admin.Name);

    private abstract class UserRoleEnum(string name, int value) : SmartEnum<UserRoleEnum>(name, value)
    {
        public static readonly UserRoleEnum Admin = new AdminType();
        public abstract Permission[] GetDefaultPermissions();

        private class AdminType() : UserRoleEnum("Admin", 0)
        {
            public override Permission[] GetDefaultPermissions() => [.. Permission.GetAll()];
        }
    }
}
```

A date that belongs to a place is a `ZonedDate`, for example a clinic day or the due date of a service at a yard. It holds the `DateOnly` and the `TimeZoneId` of the place. `TimeZoneId` accepts only an IANA ID that the server knows, such as `America/Chicago`. Linux refuses a wrong case of an ID. macOS refuses a wrong case only of a zone that it lists, not of an alias such as `Asia/Calcutta`. It refuses a Windows ID such as `Central Standard Time`. Do not store a `ZonedDate`. The entity stores the `DateOnly` and the `TimeZoneId` of its place, and makes the `ZonedDate` when it needs one. Map the `TimeZoneId` with `ComplexProperty`, and give its column `TimeZoneId.MaxLength`. List the column in `FixedOptionColumns` of `StringColumnLengthTests` with the IANA IDs of the server, because a caller can send only an ID that the server knows. Take today with `ZonedDate.TodayAt(zone, timeProvider.GetUtcNow())`, never from the UTC date. A rule that compares a date to today compares it to today at the place. `Start` is the first instant of the day. It is local midnight, or the end of a gap at midnight. `End` is the `Start` of the next day, and the day does not contain it. `TimeZoneId.ToInstant(date, time)` gives null for a time in a spring gap, and the first instant for a time in the repeated fall hour. The SPA shows and reads the times of a place in its zone with `frontend/src/lib/zoned-time.ts`. See "Dates and times" in [code-style.md](../frontend/code-style.md).

<!-- example:start -->
`WorkshopStatus` is a state machine. Each option of its SmartEnum lists the statuses that can come next, and `MoveTo` refuses each other move with a 422: [WorkshopStatus.cs:39](../../FullstackTemplate.Server/Examples/Workshops/WorkshopStatus.cs#L39). `Venue` stores a `TimeZoneId`: [Venue.cs:18](../../FullstackTemplate.Server/Examples/Venues/Venue.cs#L18).
<!-- example:end -->

To map a value object to a column, see [entity-configurations.md](entity-configurations.md#value-objects).

## Do not

- Do not give an entity public setters.
- Do not change an entity from a handler or a service. Add a method to the entity.
- Do not expose a `List<T>` that callers can change.
- Do not change a child entity without its owner. Load the owner and call a method of the owner. The save of a changed child also updates the owner row, and it throws when the owner is not loaded. To move a child to another owner, load the former owner and the new owner.
