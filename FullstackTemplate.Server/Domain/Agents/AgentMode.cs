namespace FullstackTemplate.Server.Domain.Agents;

using Ardalis.SmartEnum;
using Exceptions;

/// <summary>How the server reads a token of the client of an agent. The column holds "delegated" or "machine".</summary>
public partial class AgentMode : ValueObject
{
    private const string RequestField = "mode";

    /// <summary>The stored values, for a query or an index filter that names the mode.</summary>
    public const string DelegatedValue = "delegated";

    /// <inheritdoc cref="DelegatedValue"/>
    public const string MachineValue = "machine";

    private AgentModeEnum _mode = null!;

    public string Value
    {
        get => _mode.Name;
        private set
        {
            if (!AgentModeEnum.TryFromName(value, true, out var parsed))
                throw new ValidationException(RequestField, $"Invalid mode: {value}. Use delegated or machine.");

            _mode = parsed;
        }
    }

    public AgentMode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException(RequestField, "Please provide a mode.");

        Value = value;
    }

    /// <summary>
    /// True when the client acts for itself with client credentials. It holds its grant in the tenant of the agent.
    /// False when the client acts for a signed-in user of the tenant. It then holds only the permissions that both the
    /// user and the grant have.
    /// </summary>
    public bool IsMachine => _mode == AgentModeEnum.Machine;

    public override string ToString() => Value;

    public static AgentMode Of(string value) => new(value);
    public static AgentMode Delegated() => new(AgentModeEnum.Delegated.Name);
    public static AgentMode Machine() => new(AgentModeEnum.Machine.Name);

    protected AgentMode() { } // EF Core

    private sealed class AgentModeEnum(string name, int value) : SmartEnum<AgentModeEnum>(name, value)
    {
        public static readonly AgentModeEnum Delegated = new(DelegatedValue, 0);
        public static readonly AgentModeEnum Machine = new(MachineValue, 1);
    }
}
