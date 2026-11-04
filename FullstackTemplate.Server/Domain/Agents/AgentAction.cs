namespace FullstackTemplate.Server.Domain.Agents;

using Ardalis.SmartEnum;
using Exceptions;
using Users;

/// <summary>
/// A command that a caller can send for an agent record. Each action names the permissions that it needs, so
/// <see cref="AllowedFor"/> and <see cref="GuardCaller"/> read one table and cannot disagree. The response of each
/// command but Delete carries the record of the agent, so it also needs read_agents (ADR 0009). A permission that the
/// request names, such as the permission to grant, is not in the table. The command checks it.
/// </summary>
public partial class AgentAction : ValueObject, IFixedNames
{
    private readonly AgentActionEnum _action;

    private AgentAction(AgentActionEnum action) => _action = action;

    public string Value => _action.Name;

    public override string ToString() => Value;

    public static List<string> ListNames() => AgentActionEnum.List.OrderBy(x => x.Value).Select(x => x.Name).ToList();

    public static AgentAction AddPermission() => new(AgentActionEnum.AddPermission);
    public static AgentAction RemovePermission() => new(AgentActionEnum.RemovePermission);
    public static AgentAction Delete() => new(AgentActionEnum.Delete);

    /// <exception cref="ForbiddenAccessException">The caller lacks a permission. The message names the first one.</exception>
    public void GuardCaller(Caller caller) => caller.Require(_action.Needs);

    internal static IReadOnlyList<AgentAction> AllowedFor(Caller caller)
        => AgentActionEnum.List
            .OrderBy(action => action.Value)
            .Where(action => action.Needs.All(caller.Has))
            .Select(action => new AgentAction(action))
            .ToList();

    private sealed class AgentActionEnum : SmartEnum<AgentActionEnum>
    {
        public static readonly AgentActionEnum AddPermission = new(nameof(AddPermission), 0,
            [Permission.ManageAgents(), Permission.ReadAgents()]);

        public static readonly AgentActionEnum RemovePermission = new(nameof(RemovePermission), 1,
            [Permission.ManageAgents(), Permission.ReadAgents()]);

        public static readonly AgentActionEnum Delete = new(nameof(Delete), 2, [Permission.ManageAgents()]);

        private AgentActionEnum(string name, int value, Permission[] needs) : base(name, value) => Needs = needs;

        public Permission[] Needs { get; }
    }
}
