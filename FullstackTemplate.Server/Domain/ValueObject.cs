namespace FullstackTemplate.Server.Domain;

using System;

public abstract class ValueObject : IEquatable<ValueObject>
{
	public static bool operator ==(ValueObject? obj1, ValueObject? obj2)
	{
		return obj1?.Equals(obj2) ?? Equals(obj2, null);
	}

	public static bool operator !=(ValueObject? obj1, ValueObject? obj2)
	{
		return !(obj1 == obj2);
	}

	public bool Equals(ValueObject? obj)
	{
		return Equals(obj as object);
	}

	public abstract override bool Equals(object? obj);
	public abstract override int GetHashCode();

	// The generated Equals calls this for a member with one item type, such as an array, a list, or a set.
	protected static bool SequenceEquals<T>(IEnumerable<T>? first, IEnumerable<T>? second)
		=> first is null ? second is null : second is not null && first.SequenceEqual(second);

	// The generated GetHashCode calls this for a member with one item type, such as an array, a list, or a set.
	protected static int SequenceHashCode<T>(IEnumerable<T>? items)
	{
		if (items is null)
			return 0;

		var hash = new HashCode();
		foreach (var item in items)
			hash.Add(item);
		return hash.ToHashCode();
	}
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class IgnoreMemberAttribute : Attribute
{
}
