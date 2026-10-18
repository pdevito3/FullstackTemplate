namespace FullstackTemplate.Server.Domain.Users;

/// <summary>
/// Marks a request that manages the users or the settings of a tenant. Only an admin of the tenant,
/// or a caller that can manage all tenants (a trusted tenant management client, or system work in a system scope),
/// can send it. <see cref="TenantAdminBehavior{TRequest,TResponse}"/> enforces this before the handler runs.
/// </summary>
public interface IRequireTenantAdmin;
