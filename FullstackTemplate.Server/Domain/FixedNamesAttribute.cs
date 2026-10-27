namespace FullstackTemplate.Server.Domain;

/// <summary>
/// The string property holds one name of a fixed list, or the property is a list of such names. The OpenAPI document
/// gives the names as an enum, so the generated SPA type is a union. Use <see cref="NamesOfAttribute{T}"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public abstract class FixedNamesAttribute : Attribute
{
    public abstract IReadOnlyList<string> Names { get; }
}
