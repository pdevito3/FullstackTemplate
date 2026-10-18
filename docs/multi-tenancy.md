# Multi-tenancy

The server keeps the data of each tenant separate.

## How it works

1. An entity implements `ITenantable`, so it has a `TenantId`.
2. `AppDbContext` adds a query filter on `TenantId` for each tenantable entity.
3. `TenantIdProvider` gets the tenant of the current user from the user claims.
4. The context sets the `TenantId` of a new entity automatically.

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

The filter depends on the current user:

| Current user | Rows that the filter shows |
|--------------|----------------------------|
| No user (system work, machine client) | The rows of all tenants |
| A user with a user record | The rows of this user's tenant |
| A signed-in user with no user record | No rows |

The provider does not cache a missing user. A user who gets a user record later sees their tenant on the next request.
