namespace FullstackTemplate.Server.Domain.Tenants.DomainEvents;

public sealed record TenantUpdated(string Id) : DomainEvent;
