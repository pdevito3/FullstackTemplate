namespace FullstackTemplate.Server.Resources;

using System.Diagnostics;
using MediatR;
using ValidationException = Exceptions.ValidationException;

/// <summary>
/// Wraps every MediatR request in a span named after its feature slice, so a trace reads
/// <c>GET api/v1/users/{id}</c> → <c>GetUser.Query</c> → the tagged database spans underneath it.
/// Without this the handler is invisible and a slow request shows only its transport span and its
/// queries, with nothing naming the work in between.
/// <para>
/// Privacy invariant (see <see cref="Telemetry"/>): the span carries the request type name only. It
/// never reads a property, so no user content can reach the exporter through this behavior.
/// </para>
/// </summary>
public sealed class RequestTracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Slices nest their contract inside the feature class, so the plain type name is "Command" or
    /// "Query" for every one of them. Qualify it: <c>GetUser+Query</c> becomes "GetUser.Query".
    /// </summary>
    private static readonly string RequestName = typeof(TRequest).DeclaringType is { } feature
        ? $"{feature.Name}.{typeof(TRequest).Name}"
        : typeof(TRequest).Name;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Source.StartActivity(RequestName);
        activity?.SetTag("app.request.name", RequestName);

        try
        {
            return await next(cancellationToken);
        }
        catch (Exception exception)
        {
            activity?.SetTag("error.type", exception.GetType().FullName);

            // A validation error is the caller's fault and an expected outcome, so it is tagged but not
            // marked failed. Marking it Error would color a normal 400 as a server fault in the trace.
            if (exception is not (ValidationException or FluentValidation.ValidationException))
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);

            throw;
        }
    }
}
