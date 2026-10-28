namespace FullstackTemplate.Server.Domain.Tenants.DomainEvents;

public sealed record TenantCreated(string TenantId) : DomainEvent;
