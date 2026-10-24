namespace FullstackTemplate.Server.Resources;

using Databases;
using MediatR;

/// <summary>
/// The innermost behavior. It names the request of each save of the context of the scope, so each audit entry shows
/// the request that wrote it. It reads no property of the request.
/// </summary>
public sealed class AuditRequestBehavior<TRequest, TResponse>(AppDbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        using var requestName = dbContext.EnterRequest(RequestName<TRequest>.Value);
        return await next(cancellationToken);
    }
}
