namespace FullstackTemplate.Server.Domain.Tenants.DomainEvents;

public sealed record TenantCreated(Tenant Tenant) : DomainEvent;
