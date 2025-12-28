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

    public static void ThrowWhenNull<T>(T? value, string message) where T : class
    {
        if (value is null)
            throw new ValidationException(message);
    }

    public static void ThrowWhenNullOrWhitespace(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException(message);
    }

    public static void ThrowWhenEmpty(Guid value, string message)
    {
        if (value == Guid.Empty)
            throw new ValidationException(message);
    }

    public static void Must(bool condition, string message)
    {
        if (!condition)
            throw new ValidationException(message);
    }

    public static void MustNot(bool condition, string message)
    {
        if (condition)
            throw new ValidationException(message);
    }
}
