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
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class IgnoreMemberAttribute : Attribute
{
}
