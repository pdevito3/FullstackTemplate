# Identify agents by client ID and keep their grants as tenant rows

An agent is an OAuth client other than this app that calls the server, usually through MCP. A delegated agent acts for a signed-in user. A machine agent acts for itself with a client credentials token. The server identifies a caller from the client ID of the token (`client_id`, then `azp`, then `applicationId`) and from the `sub`. It does not take a missing `sub` to mean a machine, because Keycloak, ZeroFactor, and Cognito give a client credentials token a service-account `sub`.

`CallerIdentityResolver` applies one rule, in this order:

1. System work in a system scope is the system.
2. A request with no authenticated principal is anonymous.
3. A token with no client ID is an unknown client.
4. A token from a first-party client (`Auth:ClientId`, or `Swagger:ClientId` when it is set) with a `sub` is a user.
5. A token whose client ID is on a `Tenancy` allow-list is an all-tenants client, whatever its `sub`.
6. A token whose client ID has a live machine `Agent` record is a machine agent, whatever its `sub`.
7. A token whose `sub` is a live user of the app is a delegated agent, if the tenant of the user has a live delegated `Agent` record for the client ID.
8. Any other token is an unknown client.

An unknown client sees no rows, holds no permission, and sees no MCP tool.

An `Agent` record is in one tenant. Its grant is `AgentPermission` rows. A delegated agent holds the permissions that both its user and its grant have. A machine agent holds its grant, and it sees the rows of the tenant of its record. The permission guard reads the user rows and the agent rows from the database in each check, as [ADR 0009](0009-permission-checks-in-the-handler.md) does for users. A caller with `manage_agents` registers an agent and changes its grant. `Agent.Create` and `Agent.Grant` call `Require` on the caller, so a caller cannot give an agent a permission that the caller does not have. A revoke and a delete need only `manage_agents`. Only a caller that can manage all tenants can create a machine agent.

Each MCP tool declares what it needs with one `[ToolAccess]` attribute: a permission, the capability to manage all tenants, or a read. The filter of `tools/list` hides a tool that the caller cannot use. The filter of `tools/call` denies such a tool with a `permission_denied` tool error. Both filters ask `Caller`, the same type that the handler asks through `IPermissionGuard`. The handler still checks the permission.

`BaseEntity` records the client ID of the token in `CreatedByClient` and `LastModifiedByClient`, next to the `sub` in `CreatedBy` and `LastModifiedBy`.

> [ADR 0012](0012-audit-log.md) replaces these four columns. Each audit entry records the `sub` and the client ID of the token, and the migration `DropWhoAndWhenColumns` drops the columns. The choice of the client ID of each token, below, still applies.

## Considered options

### How to identify an agent

- **A missing `sub` means a machine**, as before. We did not choose this option. A service-account token has a `sub`, so the allow-lists never matched it. A delegated agent has the `sub` of its user, so it got each permission of the user.
- **The `act` claim of token exchange (RFC 8693).** We did not choose this option. No local provider gives it, and MCP clients do not do token exchange. The resolver is the one place to add it later.
- **A token with no client ID is first-party.** We did not choose this option. If a provider gives one audience to many clients and no client claim, the server cannot tell the clients apart, and each of them would act as the user.
- **An unknown client with a user `sub` acts as the user.** We did not choose this option. The `sub` proves that the user signed in to the client. It does not prove that the tenant accepted the client. Each client that the identity provider gives the API audience would get each permission of the user.

### Where the grant is

- **Configuration on the server**, a list of client IDs with a tenant and permissions. We did not choose this option. The app creates tenants at run time, so each agent of a tenant would need a deploy. A configuration has no grantor, so it cannot stop a grant of more than the grantor has.
- **Scopes in the token**, one scope for each permission, intersected with the rows of the user. We did not choose this option. A scope is data of the identity provider, and a withdrawn scope stays in the token until the token expires. The DuendeDemo, FusionAuth, and Cognito providers of this repository cannot give custom scopes. A scope cannot limit a machine to one tenant. Each client with the API audience could read the tenant of its user with no scope.
- **A grant on the user**, "this user lets client X use permission P". We did not choose this option. Each user must manage each agent, a machine agent has no user, and the rows grow as users times agents.
- **`Agent` rows in the tenant.** We chose this option. A grant is a decision of the tenant. It changes at the pace of the tenant, an admin of the tenant changes it, and it must apply on the next request. These are the reasons that ADR 0009 keeps permissions as rows.

### How a client ID maps to tenants

- **A delegated client ID is unique in each tenant.** The user gives the tenant, so many tenants can register one public MCP client.
- **A client ID has one machine agent in all tenants.** A machine token names no tenant, so the record must give exactly one tenant.
- **The `sub` of the token decides before the client ID.** We did not choose this option. A tenant admin writes the identifier of a user record. A tenant could add a user with the service-account `sub` of the machine agent of another tenant, and then register that client as its own delegated agent. The job of the other tenant would then read and write the first tenant. The allow-lists and the machine agents are facts of the operator about a client, so they decide first.
- **A tenant admin binds a machine client.** We did not choose this option. Tenant A could bind the client ID of the sync job of tenant B before B does. The job of B would then write into A. The operator creates the client at the identity provider, so the operator binds it. After that, an admin of the tenant narrows, widens, or deletes it.
- **Pin the service-account `sub` on a machine record.** We did not choose this option. The client credentials grant needs a confidential client (RFC 6749, section 4.4). A user can get a token for a machine client only if the operator binds a client that also allows a user sign-in, and each holder of its secret can get a machine token anyway. The operator binds only a client that allows only client credentials.

### Who can take a grant away

- **The caller must hold each permission that it takes away**, the rule for a user. We did not choose this option. A tenant admin who lacks a permission of an agent could then not stop the agent, for example a machine agent that the operator bound with `manage_users`. A revoke cannot raise the access of anyone.
- **`manage_agents` is enough to revoke or delete.** We chose this option.

### How a tool declares its need

- **The authorization filters of the SDK (`AddAuthorizationFilters`) with policies.** We did not choose this option. A denial is a JSON-RPC error, not a `permission_denied` tool error, and the rule is in a transport type (ADR 0009).
- **`[McpMeta]` on the tool.** We did not choose this option. The SDK sends the metadata to each client.
- **No declaration, and a filter that runs the check of the handler.** We did not choose this option. The check of a handler can depend on the arguments and on the data, so `tools/list` cannot run it.

### What the audit records

- **The client ID only for an agent, and null for a first-party client.** We did not choose this option. The audit would need a second copy of the first-party rule, and a null would mean both "the app" and "no token".
- **The ID of the `Agent` record.** We did not choose this option. A client on an allow-list has no record, and the client ID is the value that the identity provider logs.
- **The client ID of each token.** We chose this option. It is a fact of the token, so the audit needs no rule. The value is null only for work with no token, such as a background job.

## Consequences

- A new MCP client needs an `Agent` record in each tenant that uses it. A client that registers itself dynamically gets a new client ID for each installation, so a tenant cannot register it ahead. A pre-registered client, or a client that uses a Client ID Metadata Document URL as its client ID, can be registered.
- A removed grant row, a deleted agent, a deleted tenant, or a removed user permission applies on the next request on each instance. Each token from a client that is not first-party and not on an allow-list pays the query of the agent records, also a token from an unknown client. Nothing caches a negative answer.
- The tool attribute repeats the permission that the handler checks. If the two differ, the tool is hidden from a caller who could use it, or the handler denies the call. Neither gives more access.
- A delegated agent does not get the exception of `Caller.RequireSelfOr` for its own user.
- ADR 0009 said that a machine client cannot have a part of the permissions. A machine agent now can.
- Each token of a client on an allow-list or bound as a machine acts as that client, whatever its `sub`. The operator must use only a confidential client that allows only client credentials for both.
- A service account that has a user record, as the docs told Keycloak operators to make before, is now an unknown client. The operator binds it as a machine agent or puts it on a list.
- On MCP, an unknown client gets a `permission_denied` tool error for a read tool, which tells it to ask for a registration. Over HTTP and gRPC, the same caller gets 403 from a query that checks a permission, and an empty list or 404 from the other queries.
- Cognito accepts only the first-party client as the audience, so agents do not work with Cognito.
- The server publishes OAuth protected resource metadata (RFC 9728) for `/mcp`, so a standard MCP client finds the identity provider by itself. The fix for [issue 68](https://github.com/pdevito3/FullstackTemplate/issues/68) added it.
