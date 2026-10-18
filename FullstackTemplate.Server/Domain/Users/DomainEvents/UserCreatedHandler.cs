namespace FullstackTemplate.Server.Domain.Users.DomainEvents;

using MediatR;
using Services;

/// <summary>
/// Removes the cached tenant lookup for the new user's identifier. The cache can hold a miss
/// for this identifier, and the user must see their tenant on the next request.
/// </summary>
public sealed class UserCreatedHandler(ITenantIdProvider tenantIdProvider) : INotificationHandler<UserCreated>
{
    public async Task Handle(UserCreated notification, CancellationToken cancellationToken)
    {
        await tenantIdProvider.InvalidateCacheAsync(notification.User.Identifier, cancellationToken);
    }
}
