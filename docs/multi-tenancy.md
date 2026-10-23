# Multi-tenancy

The server keeps the data of each tenant separate.

## How it works

1. An entity implements `ITenantable`, so it has a `TenantId`.
2. `AppDbContext` adds a query filter on `TenantId` for each tenantable entity. `Tenant` gets the same filter on its `Id`.
3. `TenantScopeProvider` finds the `TenantScope` of the current request. `AppDbContext` gets this scope once and keeps it. The filter reads the kept scope for each query. If code changes the caller of a live DI scope, for example a test, call `AppDbContext.RefreshTenantScope()`.
4. The context does not set the `TenantId` of a new entity. Set it when you create the entity.

## Make an entity multi-tenant

```csharp
public class MyEntity : BaseEntity, ITenantable
{
    public const string IdPrefix = "myent";

    public string TenantId { get; private set; } = default!;
    // ... other properties

    protected MyEntity() : base(IdPrefix) { } // For EF Core
}
```

In the entity configuration, give `TenantId` the same length and collation as an ID, and relate it to `Tenant`:

```csharp
builder.Property(e => e.TenantId)
    .IsRequired()
    .HasMaxLength(EntityId.MaxLength)
    .UseCollation(EntityId.Collation);

builder.HasOne<Tenant>()
    .WithMany()
    .HasForeignKey(e => e.TenantId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasIndex(e => e.TenantId);
```

The foreign key to `tenants.id` must have the same collation as `tenants.id`.

## Tenant lookup

`ITenantIdProvider` finds the tenant from the user identifier. The lookup gives a tenant only if the user and the tenant exist and are not deleted. FusionCache keeps the result for 30 minutes. If the database is not available, the fail-safe cache keeps the last result for a maximum of 2 hours.

The provider keeps a missing user for 30 seconds, with no fail-safe. This stops a signed-in user with no user record from causing a database lookup for each query.

`AppDbContext.SaveChangesAsync` removes the stale cached results after the commit, before it returns. `TenantLookupInterceptor` does this step. You do not call the cache in a feature. The context finds the stale results in the change tracker:

- When a user is added, the context removes the cached result of its identifier. The new user sees their tenant on the next request.
- When a user is deleted, or its identifier or its tenant changes, the context removes the cached result of the old and the new identifier. The user loses access on the next request.
- When a tenant is deleted, the context removes the cached result of each user of the tenant. The users lose access on the next request.

When the context removes stale results, it adds a `tenant_lookup.removed` event to the current span. The `app.tenant_lookup.removed_count` tag has the number of identifiers. If the cache fails to remove a result, the context logs an error and the save still returns its result. The old result then stays until it expires.

A lookup can start before the save and finish after the removal. Each removal moves the identifier to a new cache key, so the late result goes to an old key that no request reads.

A write that does not go through `SaveChangesAsync`, such as `ExecuteUpdateAsync` or raw SQL, does not remove the cached results. If such a write adds a user, or changes the identifier, the tenant, or the deleted state of a user or a tenant, call `ITenantIdProvider.InvalidateCacheAsync` after the write.

The cache is in the memory of each server instance. There is no distributed cache and no backplane. The removals and the "next request" behavior above are true for one server instance only. If you run more than one instance, the other instances keep the old result until it expires. Add a backplane to FusionCache before you run more than one instance.

A `TenantScope` has one of three values. The filter makes the decision for each value. It does not depend on the data in the rows.

| Current user | Scope | Rows that the filter shows |
|--------------|-------|----------------------------|
| System work in a system scope (see below) | `TenantScope.AllTenants` | The rows of all tenants |
| A machine client on one of the two allow-lists | `TenantScope.AllTenants` | The rows of all tenants |
| A user with a user record | `TenantScope.ForTenant(id)` | The rows of this user's tenant |
| A signed-in user with no user record | `TenantScope.NoTenant` | No rows |
| No HTTP request and no system scope | `TenantScope.NoTenant` | No rows |
| Any other caller (a machine client on no allow-list, an anonymous request) | `TenantScope.NoTenant` | No rows |

## System work

Work with no HTTP request gets `TenantScope.NoTenant` by default. Examples are a hosted service, a background job, and fire-and-forget work that lost its request. This default fails closed.

If system work must see all tenants, it must opt in. Create the DI scope with `CreateSystemScope()`:

```csharp
await using var scope = scopeFactory.CreateSystemScope();
var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
```

The opt-in applies to this scope only. It does not flow into other scopes. A system scope gets `TenantScope.AllTenants`, even if the scope has a user.

These callers use a system scope:

- The integration test fixture, when it seeds the data.
- The functional test helpers in `TestBase`, which arrange and check data outside a request.
- The CSharpRepl `Send` and `InScope` helpers. `SendAs` uses a plain scope, so the filter uses the tenant of the user.
- The design-time `AppDbContextFactory` for the EF Core tools.

The migration hosted service does not use a system scope. Migrations do not read through the tenant filter.

## Domain event handlers

A domain event handler runs in the tenant of the `ITenantable` entity or the `Tenant` that queued the event. While the handler runs, the scope of the context is `TenantScope.ForTenant` of that tenant, so the filter shows the rows of this tenant only. This is also true when the caller has `TenantScope.AllTenants` or `TenantScope.NoTenant`. After the handler, the context uses the scope of the caller again. The handler of an event from an entity with no tenant runs in the scope of the caller. See [domain-events.md](backend/domain-events.md#handlers).

## Row scope and capability

The scope tells which rows a caller sees. It does not tell what a caller can do. For an access decision, use `ITenantScopeProvider.CanManageAllTenants()`, not `TenantScope.IncludesAllTenants`.

`CanManageAllTenants()` is true for a client in `ManageAllTenantsClientIds` and for system work in a system scope. It is false for all other callers, including a client in `ReadAllTenantsClientIds`.

## Add a user to a tenant

These rules stop a caller from joining a tenant that is not theirs:

- `POST /api/v1/users` needs the `manage_users` permission, and each default permission of the new role (see [backend.md](backend.md#permissions)). The server puts the new user in the tenant of the caller. The request body does not need a tenant. A `tenantId` that is empty or has only spaces is the same as no `tenantId`.
- A caller with `TenantScope.AllTenants` has no tenant of its own, so it must send `tenantId` in the body. If the tenant does not exist, the request gets 422.
- If a user sends `tenantId`, it must be their own tenant. Other requests get 403.
- `POST /api/v1/users/initiate` creates a tenant and its first user. A signed-in user can do this only for their own `sub`. A caller with no `sub` must be able to manage all tenants. Other requests get 403.
- If a user already has the identifier, `POST /api/v1/users` and `initiate` return 422. A user belongs to one tenant only.

## Tenant endpoints

The tenant filter also applies to `Tenant`, so the `/api/v1/tenants` endpoints obey the scope:

- A user can get only their own tenant. A user with the `manage_tenant` permission can update and delete only their own tenant. For other tenants, the endpoints return 404. A user without `manage_tenant` gets 403 for an update or a delete.
- The list shows only the tenant of the caller. A caller with `TenantScope.NoTenant` gets an empty list.
- Only a caller that can manage all tenants can add a tenant with `POST /api/v1/tenants`. Other requests get 403. A user starts their own tenant with `POST /api/v1/users/initiate`.

## Machine clients

A machine client gets a token with no `sub` claim. The issuer can give such a token to any client that it knows, so a missing `sub` does not make a caller trusted. Only the clients on one of two allow-lists are trusted. Both lists are empty by default.

| Setting | The client can |
|---------|----------------|
| `Tenancy:ReadAllTenantsClientIds` | Read the rows of all tenants. It cannot add, change, or delete anything. |
| `Tenancy:ManageAllTenantsClientIds` | Read the rows of all tenants, add a tenant, start a tenant for another user, and pass each permission check in any tenant. |

Put a client on the read list unless it must change data. For example, a reporting service only reads.

```json
{
  "Tenancy": {
    "ReadAllTenantsClientIds": ["reporting-service"],
    "ManageAllTenantsClientIds": ["provisioning-service"]
  }
}
```

The server compares the `client_id` claim (or `azp`) with the lists. The comparison is case-sensitive.

Keycloak gives a service account token a `sub` claim. The server handles this token as a user, so the service account needs a user record in a tenant.
