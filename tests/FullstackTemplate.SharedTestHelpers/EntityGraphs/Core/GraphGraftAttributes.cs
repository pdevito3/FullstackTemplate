namespace FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;

/// <summary>Generates typed Add graft methods for blueprint members in a static catalog.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GraphGraftAttribute : Attribute { }

/// <summary>Overrides the generated singular graft name or plural suffix.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
public sealed class GraftAttribute(string? name = null) : Attribute
{
    /// <summary>Gets the optional name used after the generated Add prefix.</summary>
    public string? Name { get; } = name;

    /// <summary>Gets or sets the optional plural name used after the generated Add prefix.</summary>
    public string? Plural { get; set; }
}

/// <summary>Excludes a blueprint catalog member from generated Add graft methods.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
public sealed class NoGraftAttribute : Attribute { }
