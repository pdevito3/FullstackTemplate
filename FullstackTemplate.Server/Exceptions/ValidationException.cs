namespace FullstackTemplate.Server.Exceptions;

/// <summary>
/// Exception thrown when domain validation rules are violated.
/// </summary>
public class ValidationException : Exception
{
    public string Property { get; }

    public ValidationException(string message)
        : base(message)
    {
        Property = string.Empty;
    }

    public ValidationException(string property, string message)
        : base(message)
    {
        Property = property;
    }

    public ValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Property = string.Empty;
    }

    // The property is the name of the request field. It is the key in the errors of the problem.
    public static void ThrowWhenNull<T>(T? value, string property, string message) where T : class
    {
        if (value is null)
            throw new ValidationException(property, message);
    }

    public static void ThrowWhenNullOrWhitespace(string? value, string property, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException(property, message);
    }

    // Postgres counts the length of a varchar in code points, and a code point outside the Basic Multilingual Plane is two chars.
    public static void ThrowWhenLongerThan(string value, int maxLength, string property, string message)
    {
        if (value.EnumerateRunes().Count() > maxLength)
            throw new ValidationException(property, message);
    }

    public static void Must(bool condition, string property, string message)
    {
        if (!condition)
            throw new ValidationException(property, message);
    }

    public static void MustNot(bool condition, string property, string message)
    {
        if (condition)
            throw new ValidationException(property, message);
    }
}
