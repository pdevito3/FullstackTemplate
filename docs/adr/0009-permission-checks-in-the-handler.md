# Check permissions in the handler, from the database

Each command handler that changes the data of a tenant calls `IPermissionGuard` as its first statement, for example `await permissionGuard.RequireAsync(Permission.ManageUsers(), cancellationToken)`. Each query handler that reads the data of other users or of the agents also checks a read permission, `read_users` or `read_agents`, as `GetAuditEntryPage` checks `read_audit_log`. A user reads their own record with no permission: `GetUser` calls `RequireSelfOrAsync`, and `GetUserByIdentifier` makes the same exception after it loads the user. The guard reads the user of the caller and their `UserPermission` rows from the database in each handler that checks, and throws `ForbiddenAccessException` when the permission is missing. The decision is in `Caller`, a pure type with three cases: a user with a user record, a caller that can manage all tenants, and a caller with no permissions. Each case gives its permissions as a read-only set, and `Caller.Has` reads that set. The checks that throw also call `Has`, so there is one decision. `IRequireTenantAdmin`, `IAllowSelf`, and `TenantAdminBehavior` are deleted.

A handler is shared by HTTP, gRPC, and MCP. A check in the handler thus applies to each transport, and each transport maps the exception: 403 with problem details, `PermissionDenied`, or a `permission_denied` tool error.

## Considered options

### Where the check runs

- **A marker interface and a MediatR behavior**, the old `IRequireTenantAdmin` with a permission on it. We did not choose this option. A reader of the handler does not see the rule. An exception for the user that the command is about needs a second marker, which was `IAllowSelf`. A behavior also cannot express "require each permission that the command gives".
- **ASP.NET Core authorization policies on the endpoints.** We did not choose this option. A policy is a transport type, so HTTP, gRPC, and MCP each need a copy of the rule, and a request that a job sends has no check.
- **Give the `Caller` to each handler**, so that the handler loads it and then calls `caller.Require`. We did not choose this as the usual form, because each check then needs two statements. `RequireAsync` is one statement. Some rules need the full set, for example an entity rule that depends on the state of the entity and on a permission. For these rules, `IPermissionGuard.GetCallerAsync` gives the `Caller`, and the handler gives `caller.Permissions` to the entity method. `Permissions` and `Has` are on the base type, so a handler does not branch on the case of the caller. A later filter, such as a filter of the MCP tool list, can ask `caller.Has` and get the same answer as `RequireAsync`.

### Where the permissions come from

- **Claims in the access token.** We did not choose this option. The permissions are rows that the app changes, not data of the identity provider. Each of the auth providers would need a claim mapping, and a removed permission would stay in the token until it expires.
- **A cache, like the tenant lookup in `TenantIdProvider`.** We did not choose this option. The FusionCache has no backplane, so a removed permission stays valid on the other server instances until the entry expires. Each write that changes a permission, a role, or a user must also remove the entry. For an access decision, this risk costs more than the query that it saves.
- **Read the database for each check.** We chose this option. It is one indexed query, and only the handlers that check a permission run it. A removed permission, a changed role, or a deleted user applies on the next request on each instance, with no invalidation code.

### The permissions

The placeholder `do_something_special` is replaced by `manage_users`, `manage_user_access`, and `manage_tenant`. `manage_user_access` is separate from `manage_users`, because it changes what a user can do. A caller can give or take away only the permissions that it has. Thus a user with only `manage_users` cannot add or delete an `Admin`, and a user with only `manage_user_access` cannot change the role of an `Admin`. Without the second half, such a user could take each permission away from each admin, and nobody in the tenant could give them back. `manage_tenant` gives both the rename and the delete of the tenant. If a rename must be given alone, it needs a permission of its own.

`read_users` gives the list and the records of the other users of the tenant, with their email, role, and permissions. `read_agents` gives the list and the records of the agents, with their client IDs and grants. Before these permissions, each signed-in user could read all of this data. They are separate from `manage_users` and `manage_agents`, so that a user can read the data and not change it. A manage permission does not give the read permission. A query whose rows each member can see, such as the tenant of the caller, has no check. The tenant filter still limits the rows of each query.

`AddTenant` and `InitiateUserAndTenant` keep their check that the caller can manage all tenants. They act outside one tenant, and a permission row is in one tenant. If this capability were a permission, an admin could give it to themselves.

## Consequences

- A new command can omit the check, and no test fails. With the marker, a command could also omit the marker. A build check that each command handler takes `IPermissionGuard`, or is on a list of exceptions, can close this gap later.
- The rows decide, not the role. Thus the migration `ReplacePlaceholderPermissions` gives the new permissions to the admins that exist, and deletes the placeholder rows. `Permission` cannot load a name that it does not know, so a row with the old name stops each load of its user. In a rolling deploy, an instance with the old code also cannot load a user with a row of a new permission, until that instance stops. The server applies the migrations before it accepts requests, so an instance with the new code is safe. Each new permission needs a migration that calls `GrantPermissionToRole`.
- The migration `GrantReadPermissions` gives `read_users` and `read_agents` to the admins that exist. It also gives `read_users` to each user with `manage_users` or `manage_user_access`, and `read_agents` to each user with `manage_agents`, with `GrantPermissionToHoldersOf`. Without it, a user who was given `manage_users` could still change the users, but could not read the list of the users that they manage.
- A client in `ManageAllTenantsClientIds` passes each permission check, also for a permission that is added later. A machine client cannot have a part of the permissions. Thus a client in `ReadAllTenantsClientIds` gets 403 for each query that checks a read permission.
- MCP lists each tool for each caller. A call without the permission gets a tool error. `tools/list` does not filter by permission yet. A filter must ask `Caller.Has`, so that it does not put a second copy of the rule in the transport.
- If a profile shows that the query costs too much, a cache can go behind `IPermissionGuard` with no change to the handlers. It then needs a backplane, for the reason above.
