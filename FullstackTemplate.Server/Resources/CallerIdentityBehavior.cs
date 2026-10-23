namespace FullstackTemplate.Server.Resources;

using MediatR;
using Services;

/// <summary>
/// Resolves the caller before the handler runs. The tenant query filter reads
/// <see cref="ICallerIdentityResolver.Current"/> synchronously, and the first read in a DI scope blocks a thread on
/// the identity lookups. This behavior awaits <see cref="ICallerIdentityResolver.GetAsync"/> first, with the
/// cancellation token of the request, so the filter of each query in the handler reads a finished resolution.
/// </summary>
public sealed class CallerIdentityBehavior<TRequest, TResponse>(ICallerIdentityResolver callerIdentityResolver)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        await callerIdentityResolver.GetAsync(cancellationToken);
        return await next(cancellationToken);
    }
}
