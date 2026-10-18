namespace FullstackTemplate.Server.Domain.Users;

/// <summary>
/// Marks a request that manages the users or the settings of a tenant. Only an admin of the tenant,
/// or a caller that sees all tenants (an allow-listed machine client, or system work with no HTTP request),
/// can send it. <see cref="TenantAdminBehavior{TRequest,TResponse}"/> enforces this before the handler runs.
/// </summary>
public interface IRequireTenantAdmin;
