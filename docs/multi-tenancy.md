# Multi-tenancy

The server keeps the data of each tenant separate.

## How it works

1. An entity gets its tenant in one of two ways. It implements `ITenantable`, so it has a `TenantId`. Or it has no `TenantId`, and its configuration calls `ApplyTenanting` with a navigation path to a `TenantId`.
2. `AppDbContext` adds a query filter to each entity that has a tenant. The filter compares the `TenantId` or the navigation path with the scope of the caller. `Tenant` gets the same filter on its `Id`.
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
    .OnDelete(DeleteBehavior.ClientNoAction);

builder.HasIndex(e => e.TenantId);
```

The foreign key to `tenants.id` must have the same collation as `tenants.id`.

## Make a child entity multi-tenant

A child entity can get its tenant through its owner. In the configuration of the child, give the path to the `TenantId` of the owner:

```csharp
builder.ApplyTenanting(line => line.Order.TenantId);
```

The filter then shows a row only if the owner is in the tenant of the caller. The path must go through navigation properties, so give the child a navigation to its owner. Make the foreign key to the owner `Cascade`. The audit log reads the tenant of a change from the aggregate root, and the `Cascade` foreign key makes the owner the root (see [audit-log.md](backend/audit-log.md)). An entity that implements `ITenantable` must be a root. `UserPermission` is an example. The context adds the tenant filter when the model is complete, so a context that calls `ApplyTenanting` after the model of `AppDbContext` also gets the tenant filter. A child that gets its tenant this way cannot queue a domain event. The save throws a `NotSupportedException`. See [domain-events.md](backend/domain-events.md#handlers).

## Entities with no tenant filter

`TenantQueryFilterTests` checks the model of `AppDbContext`. Each entity must have the tenant filter, or be on a list in the test that gives the reason why it has no tenant filter. A new entity with no filter fails the test, so you must make a decision for it:

- If the entity holds the data of a tenant, implement `ITenantable` or call `ApplyTenanting`.
- If the entity has no tenant data, such as the replay cache of the DPoP check, add it to the list in the test. Each audit entry has a tenant, so such an entity cannot be a `BaseEntity`. Make it a class with `[NotAudited(reason)]`, as `UsedDPoPProof` is.

An entity with a foreign key to an entity that has the tenant filter holds the data of that tenant. The test fails for such an entity if it has no tenant filter, also when it is on the list.

A query on an entity with no tenant filter shows the rows of all tenants.

## Tenant lookup

`ITenantIdProvider` finds the tenant from the user identifier. The lookup gives a tenant only if the user and the tenant exist and are not deleted. FusionCache keeps the result for 30 minutes. If the database is not available, the fail-safe cache keeps the last result for a maximum of 2 hours.

The provider keeps a missing user for 30 seconds, with no fail-safe. This stops a signed-in user with no user record from causing a database lookup for each query.

`AppDbContext.SaveChangesAsync` removes the stale cached results after the commit, before it returns. `TenantLookupInterceptor` does this step. You do not call the cache in a feature. The context finds the stale results in the change tracker:

- When a user is added, the context removes the cached result of its identifier. The new user sees their tenant on the next request.
- When a user is deleted, or its identifier or its tenant changes, the context removes the cached result of the old and the new identifier. The user loses access on the next request.
- When a deleted user signs up again, the new user has the identifier of the deleted user. The context removes the cached result of the identifier when it adds the new user, so the next request finds the new user and its tenant.
- When a tenant is deleted, the context removes the cached result of each user of the tenant. The users lose access on the next request.

When the context removes stale results, it adds a `tenant_lookup.removed` event to the current span. The `app.tenant_lookup.removed_count` tag has the number of identifiers. If the cache fails to remove a result, the context logs an error and the save still returns its result. The old result then stays until it expires.

A lookup can start before the save and finish after the removal. Each removal moves the identifier to a new cache key, so the late result goes to an old key that no request reads.

`BulkInsertAsync` raises the same save events, so it also removes the cached result of each new user. The Server writes `users` and `tenants` only through these two. A write from outside the Server, such as SQL in a migration, does not remove the cached results. If such a write adds a user, or changes the identifier, the tenant, or the deleted state of a user or a tenant, call `ITenantIdProvider.InvalidateCacheAsync` after the write, or wait until the results expire.

The cache is in the memory of each server instance. There is no distributed cache and no backplane. The removals and the "next request" behavior above are true for one server instance only. If you run more than one instance, the other instances keep the old result until it expires. For example, after a user is deleted and signs up again, another instance can still give the deleted user and its tenant for the identifier. Until the result expires, the new user on that instance reads the rows of the old tenant that need no permission, each permission check gives 403, and the audit log names the deleted user. Add a backplane to FusionCache before you run more than one instance.

A `TenantScope` has one of three values. The filter makes the decision for each value. It does not depend on the data in the rows.

| Current caller | Scope | Rows that the filter shows |
|--------------|-------|----------------------------|
| System work in a system scope (see below) | `TenantScope.AllTenants` | The rows of all tenants |
| A client on one of the two allow-lists, whatever the `sub` of the token | `TenantScope.AllTenants` | The rows of all tenants |
| A user of a first-party client with a user record | `TenantScope.ForTenant(id)` | The rows of this user's tenant |
| A delegated agent (see [Agents](authentication.md#agents)) | `TenantScope.ForTenant(id)` | The rows of the tenant of its user |
| A machine agent | `TenantScope.ForTenant(id)` | The rows of the tenant of its agent record |
| A user of a first-party client with no user record | `TenantScope.NoTenant` | No rows |
| No HTTP request and no system scope | `TenantScope.NoTenant` | No rows |
| Any other caller (an unknown client, an anonymous request) | `TenantScope.NoTenant` | No rows |

`CallerIdentityResolver` decides the caller once for each DI scope. `TenantScopeProvider`, the permission guard, and `InitiateUserAndTenant` read the same answer. EF Core reads the tenant filter synchronously, so `CallerIdentityBehavior` resolves the caller before each MediatR handler. A query outside a MediatR request blocks a thread on the lookups if it is the first read in its scope.

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

The scope tells which rows a caller sees. It does not tell what a caller can do. For an access decision, load the caller with `IPermissionGuard.GetCallerAsync` and use `Caller.ManagesAllTenants` or `Caller.RequireManagesAllTenants()`, not `TenantScope.IncludesAllTenants`.

`ManagesAllTenants` is true for a client in `ManageAllTenantsClientIds` and for system work in a system scope. It is false for all other callers, including a client in `ReadAllTenantsClientIds`.

## Add a user to a tenant

These rules stop a caller from joining a tenant that is not theirs:

- `POST /api/v1/users` needs the `manage_users` permission, and each default permission of the new role (see [backend.md](backend.md#permissions)). The server puts the new user in the tenant of the caller. The request body does not need a tenant. A `tenantId` that is empty or has only spaces is the same as no `tenantId`.
- A caller with `TenantScope.AllTenants` has no tenant of its own, so it must send `tenantId` in the body. If the tenant does not exist, the request gets 422.
- If a user sends `tenantId`, it must be their own tenant. Other requests get 403.
- `POST /api/v1/users/initiate` creates a tenant and its first user. A user of a first-party client can do this only for their own `sub`. Any other caller, an agent included, must be able to manage all tenants. Other requests get 403.
- If a user that is not deleted already has the identifier, in any tenant, `POST /api/v1/users` and `initiate` return 409. A user belongs to one tenant only. A deleted user frees its identifier, so the same person can sign up again. A tenant delete does not delete the users of the tenant, so they keep their identifiers. As a result, `initiate` returns 409 for these users, and they cannot sign up again.

## Tenant endpoints

The tenant filter also applies to `Tenant`, so the `/api/v1/tenants` endpoints obey the scope:

- A user can get only their own tenant. A user with the `manage_tenant` permission can update and delete only their own tenant. For other tenants, the endpoints return 404. A user without `manage_tenant` gets 403 for an update or a delete.
- The list shows only the tenant of the caller. A caller with `TenantScope.NoTenant` gets an empty list.
- Only a caller that can manage all tenants can add a tenant with `POST /api/v1/tenants`. Other requests get 403. A user starts their own tenant with `POST /api/v1/users/initiate`.

## Clients for all tenants

A client on one of two allow-lists acts for the operator in all tenants. Both lists are empty by default. To give a client the rows of one tenant only, register it as a machine agent instead (see [Agents](authentication.md#agents)).

| Setting | The client can |
|---------|----------------|
| `Tenancy:ReadAllTenantsClientIds` | Read the rows of all tenants that need no permission, such as the tenants. It holds no permission, so a query that checks a permission gives 403, for example the users, the agents, and the audit log. It cannot add, change, or delete anything. |
| `Tenancy:ManageAllTenantsClientIds` | Read the rows of all tenants, add a tenant, start a tenant for another user, add a machine agent, and pass each permission check in any tenant. |

Put a client on the read list unless it must change data or read data that needs a permission. For example, a reporting service that counts the tenants only reads.

```json
{
  "Tenancy": {
    "ReadAllTenantsClientIds": ["reporting-service"],
    "ManageAllTenantsClientIds": ["provisioning-service"]
  }
}
```

The server compares the client ID of the token (`client_id`, then `azp`, then `applicationId`) with the lists. The comparison is case-sensitive. A list is a fact about the client, so it matches each token of the client, whatever its `sub`. Thus a client credentials token with a service-account `sub` from Keycloak or ZeroFactor matches, and a tenant cannot take the client over with a user record that has the same `sub`.

Put only a confidential client that allows only the client credentials grant on a list. If the client also allows a user sign-in, each user who signs in through it acts for the operator in all tenants.

On Cognito, the lists do not work: the server accepts only tokens of the first-party client (see [Providers](authentication.md#providers)).

### Upgrade from a service account with a user record

Before agents, the server took a Keycloak service account token as a user, so the docs told you to give the service account a user record. Such a token is now an unknown client and sees no rows. Do one of these:

- To give the client one tenant, bind it as a machine agent of that tenant (see [Register an agent](authentication.md#register-an-agent)), and delete its user record.
- To give the client all tenants, put it on a list.
