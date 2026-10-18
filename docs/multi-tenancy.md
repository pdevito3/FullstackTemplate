# Multi-tenancy

The server keeps the data of each tenant separate.

## How it works

1. An entity implements `ITenantable`, so it has a `TenantId`.
2. `AppDbContext` adds a query filter on `TenantId` for each tenantable entity.
3. `TenantScopeProvider` finds the `TenantScope` of the current request. The filter reads this scope for each query.
4. The context does not set the `TenantId` of a new entity. Set it when you create the entity.

## Make an entity multi-tenant

```csharp
public class MyEntity : BaseEntity, ITenantable
{
    public Guid TenantId { get; set; }
    // ... other properties
}
```

## Tenant lookup

`ITenantIdProvider` finds the tenant from the user identifier. FusionCache keeps the result for 30 minutes. If the database is not available, the fail-safe cache keeps the last result for a maximum of 2 hours.

The provider does not cache a missing user. A user who gets a user record later sees their tenant on the next request.

A `TenantScope` has one of three values. The filter makes the decision for each value. It does not depend on the data in the rows.

| Current user | Scope | Rows that the filter shows |
|--------------|-------|----------------------------|
| No HTTP request (startup work, background work, the REPL) | `TenantScope.AllTenants` | The rows of all tenants |
| A machine client on the allow-list | `TenantScope.AllTenants` | The rows of all tenants |
| A user with a user record | `TenantScope.ForTenant(id)` | The rows of this user's tenant |
| A signed-in user with no user record | `TenantScope.NoTenant` | No rows |
| Any other caller (a machine client not on the allow-list, an anonymous request) | `TenantScope.NoTenant` | No rows |

## Machine clients

A machine client gets a token with no `sub` claim. The issuer can give such a token to any client that it knows, so a missing `sub` does not make a caller trusted. Only the clients in `Tenancy:AllTenantClientIds` see the rows of all tenants. The list is empty by default.

```json
{
  "Tenancy": {
    "AllTenantClientIds": ["reporting-service"]
  }
}
```

The server compares the `client_id` claim (or `azp`) with the list. The comparison is case-sensitive.

Keycloak gives a service account token a `sub` claim. The server handles this token as a user, so the service account needs a user record in a tenant.
