namespace FullstackTemplate.Server.Domain;

public sealed class NamesOfAttribute<T> : FixedNamesAttribute where T : IFixedNames
{
    public override IReadOnlyList<string> Names => T.ListNames();
}
