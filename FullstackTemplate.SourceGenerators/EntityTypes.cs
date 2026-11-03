namespace FullstackTemplate.SourceGenerators;

using System.Linq;
using Microsoft.CodeAnalysis;

internal sealed class EntityTypes(INamedTypeSymbol baseEntity)
{
	private const string PrefixName = "IdPrefix";

	public static EntityTypes? From(Compilation compilation)
		=> compilation.GetTypeByMetadataName("FullstackTemplate.Server.Domain.BaseEntity") is { } baseEntity
			? new EntityTypes(baseEntity)
			: null;

	public bool IsEntity(INamedTypeSymbol symbol)
	{
		if (symbol.TypeKind != TypeKind.Class)
			return false;

		var current = symbol.BaseType;
		while (current is not null)
		{
			if (SymbolEqualityComparer.Default.Equals(current, baseEntity))
				return true;
			current = current.BaseType;
		}

		return false;
	}

	public static IFieldSymbol? FindPrefixField(INamedTypeSymbol symbol)
		=> symbol.GetMembers(PrefixName)
			.OfType<IFieldSymbol>()
			.FirstOrDefault(static f => f is { IsConst: true, DeclaredAccessibility: Accessibility.Public, Type.SpecialType: SpecialType.System_String });

	/// <summary>
	/// Returns the first base type between the entity and BaseEntity that is concrete or declares an IdPrefix, or null.
	/// </summary>
	public string? FindEntityBase(INamedTypeSymbol symbol)
	{
		var current = symbol.BaseType;
		while (current is not null && !SymbolEqualityComparer.Default.Equals(current, baseEntity))
		{
			if (!current.IsAbstract || current.GetMembers(PrefixName).Length > 0)
				return current.ToDisplayString();
			current = current.BaseType;
		}

		return null;
	}
}
