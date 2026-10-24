namespace FullstackTemplate.Server.Resources;

using System.Diagnostics;
using Exceptions;
using MediatR;

/// <summary>
/// Wraps every MediatR request in a span named after its feature slice, and records its duration in
/// <see cref="Telemetry.RequestDuration"/>. A trace then reads
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
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Source.StartActivity(RequestName<TRequest>.Value);
        activity?.SetTag("app.request.name", RequestName<TRequest>.Value);
        var startedAt = Stopwatch.GetTimestamp();
        string? errorType = null;

        try
        {
            return await next(cancellationToken);
        }
        catch (Exception exception)
        {
            errorType = exception.GetType().FullName;
            activity?.SetTag("error.type", errorType);

            // A client error (validation, not found) is an expected outcome, so it is tagged but not
            // marked failed. Marking it Error would color a normal 4xx as a server fault in the trace.
            if (!DomainError.IsClientError(exception))
                Telemetry.RecordError(activity, exception);

            throw;
        }
        finally
        {
            TagList tags = new() { { "app.request.name", RequestName<TRequest>.Value } };
            if (errorType is not null)
                tags.Add("error.type", errorType);
            Telemetry.RequestDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds, tags);
        }
    }
}
