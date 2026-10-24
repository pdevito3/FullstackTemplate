namespace FullstackTemplate.Server.Resources;

/// <summary>
/// The name of a MediatR request type. Slices nest their contract inside the feature class, so the plain type name is
/// "Command" or "Query" for every one of them. Qualify it: <c>GetUser+Query</c> becomes "GetUser.Query".
/// The span of <see cref="RequestTracingBehavior{TRequest,TResponse}"/> and the audit log read this one value.
/// </summary>
internal static class RequestName<TRequest>
{
    public static readonly string Value = typeof(TRequest).DeclaringType is { } feature
        ? $"{feature.Name}.{typeof(TRequest).Name}"
        : typeof(TRequest).Name;
}
