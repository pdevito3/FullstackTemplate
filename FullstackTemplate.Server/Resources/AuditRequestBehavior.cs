namespace FullstackTemplate.Server.Resources;

using Databases;
using MediatR;

/// <summary>
/// The innermost behavior. It names the request of each save of the context of the scope, so each audit entry shows
/// the request that wrote it. After the outermost request of the scope succeeds, it throws when an audit action was
/// recorded and not saved. It reads no property of the request.
/// </summary>
public sealed class AuditRequestBehavior<TRequest, TResponse>(AppDbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        using var entered = dbContext.EnterRequest(RequestName<TRequest>.Value);
        var response = await next(cancellationToken);
        if (entered.IsOutermost && dbContext.HasUnsavedActions)
            throw new InvalidOperationException(
                $"{RequestName<TRequest>.Value} recorded an audit action and did not call SaveChangesAsync. " +
                "Save it, also when the handler changes no data.");

        return response;
    }
}
