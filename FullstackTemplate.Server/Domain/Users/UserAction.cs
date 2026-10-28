namespace FullstackTemplate.Server.Domain.Users;

using Ardalis.SmartEnum;
using Exceptions;

/// <summary>
/// A command that a caller can send for a user record. Each action names the permissions that it needs, so
/// <see cref="User.AllowedActions"/> and <see cref="User.GuardAllows"/> read one table and cannot disagree.
/// A permission that the request names, such as the new role, is not in the table. The command checks it.
/// </summary>
public partial class UserAction : ValueObject, IFixedNames
{
    private readonly UserActionEnum _action;

    private UserAction(UserActionEnum action) => _action = action;

    public string Value => _action.Name;

    public override string ToString() => Value;

    public static List<string> ListNames() => UserActionEnum.List.Select(x => x.Name).ToList();

    public static UserAction Update() => new(UserActionEnum.Update);
    public static UserAction UpdateRole() => new(UserActionEnum.UpdateRole);
    public static UserAction AddPermission() => new(UserActionEnum.AddPermission);
    public static UserAction RemovePermission() => new(UserActionEnum.RemovePermission);
    public static UserAction Delete() => new(UserActionEnum.Delete);

    /// <summary>
    /// Checks the permissions that need only the ID of the user. Call it before the load, so a caller without them
    /// gets a 403 for any ID. Then call <see cref="User.GuardAllows"/> after the load.
    /// </summary>
    /// <exception cref="ForbiddenAccessException">The caller lacks a permission. The message names the first one.</exception>
    public void GuardBeforeLoad(Caller caller, string userId) => caller.Require(_action.CallerNeeds(caller, userId));

    /// <summary>The actions that the caller can send for the user, in the order of the table.</summary>
    internal static IReadOnlyList<UserAction> AllowedFor(
        Caller caller, string userId, IReadOnlyCollection<Permission> userPermissions)
        => UserActionEnum.List
            .OrderBy(action => action.Value)
            .Where(action => action.Needs(caller, userId, userPermissions).All(caller.Has))
            .Select(action => new UserAction(action))
            .ToList();

    /// <exception cref="ForbiddenAccessException">The caller lacks a permission. The message names the first one.</exception>
    internal void Guard(Caller caller, string userId, IReadOnlyCollection<Permission> userPermissions)
        => caller.Require(_action.Needs(caller, userId, userPermissions));

    private sealed class UserActionEnum : SmartEnum<UserActionEnum>
    {
        // The response of each command but Delete carries the record of the user, so another caller also needs
        // read_users (ADR 0009). The user can edit their own record with no permission.
        public static readonly UserActionEnum Update = new(nameof(Update), 0,
            always: [], unlessOwnRecord: [Permission.ManageUsers(), Permission.ReadUsers()], revokesUserPermissions: false);

        public static readonly UserActionEnum UpdateRole = new(nameof(UpdateRole), 1,
            always: [Permission.ManageUserAccess()], unlessOwnRecord: [Permission.ReadUsers()], revokesUserPermissions: true);

        public static readonly UserActionEnum AddPermission = new(nameof(AddPermission), 2,
            always: [Permission.ManageUserAccess()], unlessOwnRecord: [Permission.ReadUsers()], revokesUserPermissions: false);

        public static readonly UserActionEnum RemovePermission = new(nameof(RemovePermission), 3,
            always: [Permission.ManageUserAccess()], unlessOwnRecord: [Permission.ReadUsers()], revokesUserPermissions: false);

        public static readonly UserActionEnum Delete = new(nameof(Delete), 4,
            always: [Permission.ManageUsers()], unlessOwnRecord: [], revokesUserPermissions: true);

        private readonly Permission[] _always;
        private readonly Permission[] _unlessOwnRecord;
        private readonly bool _revokesUserPermissions;

        private UserActionEnum(
            string name, int value, Permission[] always, Permission[] unlessOwnRecord, bool revokesUserPermissions)
            : base(name, value)
        {
            _always = always;
            _unlessOwnRecord = unlessOwnRecord;
            _revokesUserPermissions = revokesUserPermissions;
        }

        public Permission[] CallerNeeds(Caller caller, string userId)
            => caller.IsUser(userId) ? _always : [.. _always, .. _unlessOwnRecord];

        public IEnumerable<Permission> Needs(Caller caller, string userId, IReadOnlyCollection<Permission> userPermissions)
            => _revokesUserPermissions ? CallerNeeds(caller, userId).Concat(userPermissions) : CallerNeeds(caller, userId);
    }
}
