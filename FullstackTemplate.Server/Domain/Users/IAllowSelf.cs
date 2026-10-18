namespace FullstackTemplate.Server.Domain.Users;

/// <summary>
/// Marks a request with <see cref="IRequireTenantAdmin"/> that a user can also send for their own record.
/// <see cref="TenantAdminBehavior{TRequest,TResponse}"/> lets the request through when the caller is the user
/// with <see cref="SelfUserId"/>.
/// </summary>
public interface IAllowSelf
{
    string SelfUserId { get; }
}
