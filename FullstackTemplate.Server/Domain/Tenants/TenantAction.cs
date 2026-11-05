namespace FullstackTemplate.Server.Domain.Tenants;

using Ardalis.SmartEnum;
using Exceptions;
using Users;

/// <summary>
/// A command that a caller can send for a tenant. <see cref="Tenant.AllowedActions"/> and the command read one table,
/// so a button in the SPA and the handler cannot disagree. A permission that the request names, such as the role of
/// an invite, is not in the table. The command checks it.
/// </summary>
public partial class TenantAction : ValueObject, IFixedNames
{
    private readonly TenantActionEnum _action;

    private TenantAction(TenantActionEnum action) => _action = action;

    public string Value => _action.Name;

    public override string ToString() => Value;

    public static List<string> ListNames() => TenantActionEnum.List.OrderBy(x => x.Value).Select(x => x.Name).ToList();

    public static TenantAction InviteUser() => new(TenantActionEnum.InviteUser);

    /// <summary>Call it before the load, so a caller without the permission gets a 403 for any tenant.</summary>
    /// <exception cref="ForbiddenAccessException">The caller lacks the permission.</exception>
    public void GuardCaller(Caller caller) => caller.Require(_action.Permission);

    internal static IReadOnlyList<TenantAction> AllowedFor(Caller caller)
        => TenantActionEnum.List
            .OrderBy(action => action.Value)
            .Where(action => caller.Has(action.Permission))
            .Select(action => new TenantAction(action))
            .ToList();

    private sealed class TenantActionEnum : SmartEnum<TenantActionEnum>
    {
        public static readonly TenantActionEnum InviteUser = new(nameof(InviteUser), 0, Permission.ManageUsers());

        private TenantActionEnum(string name, int value, Permission permission) : base(name, value)
            => Permission = permission;

        public Permission Permission { get; }
    }
}
