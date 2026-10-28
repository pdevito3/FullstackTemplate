namespace FullstackTemplate.Server.Examples.Workshops;

using Ardalis.SmartEnum;
using Domain;
using Domain.Users;
using Exceptions;

/// <summary>
/// A command that a caller can send for a workshop. Each action names its permission and the statuses that it applies
/// to, so <see cref="Workshop.AllowedActions"/> and the commands read one table.
/// </summary>
public partial class WorkshopAction : ValueObject, IFixedNames
{
    private readonly WorkshopActionEnum _action;

    private WorkshopAction(WorkshopActionEnum action) => _action = action;

    public string Value => _action.Name;

    public override string ToString() => Value;

    public static List<string> ListNames() => WorkshopActionEnum.List.OrderBy(x => x.Value).Select(x => x.Name).ToList();

    public static WorkshopAction Update() => new(WorkshopActionEnum.Update);
    public static WorkshopAction Schedule() => new(WorkshopActionEnum.Schedule);
    public static WorkshopAction Cancel() => new(WorkshopActionEnum.Cancel);
    public static WorkshopAction Complete() => new(WorkshopActionEnum.Complete);

    /// <summary>
    /// Checks the permission of the action. It needs no workshop, so call it before the load, and a caller without it
    /// gets a 403 for any ID. The workshop method of the action checks the status.
    /// </summary>
    /// <exception cref="ForbiddenAccessException">The caller lacks the permission.</exception>
    public void GuardCaller(Caller caller) => caller.Require(_action.Permission);

    /// <summary>The actions that the caller can send for a workshop in <paramref name="status"/>, in the order of the table.</summary>
    internal static IReadOnlyList<WorkshopAction> AllowedFor(Caller caller, WorkshopStatus status)
        => WorkshopActionEnum.List
            .OrderBy(action => action.Value)
            .Where(action => caller.Has(action.Permission) && action.AppliesTo(status))
            .Select(action => new WorkshopAction(action))
            .ToList();

    private abstract class WorkshopActionEnum(string name, int value) : SmartEnum<WorkshopActionEnum>(name, value)
    {
        public static readonly WorkshopActionEnum Update = new UpdateType();
        public static readonly WorkshopActionEnum Schedule = new MoveType(nameof(Schedule), 1, WorkshopStatus.Scheduled());
        public static readonly WorkshopActionEnum Cancel = new MoveType(nameof(Cancel), 2, WorkshopStatus.Cancelled());
        public static readonly WorkshopActionEnum Complete = new MoveType(nameof(Complete), 3, WorkshopStatus.Completed());

        public Permission Permission { get; } = Permission.ManageWorkshops();

        public abstract bool AppliesTo(WorkshopStatus status);

        private sealed class UpdateType() : WorkshopActionEnum(nameof(Update), 0)
        {
            public override bool AppliesTo(WorkshopStatus status) => status.CanBeEdited;
        }

        private sealed class MoveType(string name, int value, WorkshopStatus target) : WorkshopActionEnum(name, value)
        {
            public override bool AppliesTo(WorkshopStatus status) => status.CanMoveTo(target);
        }
    }
}
