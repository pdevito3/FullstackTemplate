# FullstackTemplate

A multi-tenant web app template. A browser talks only to the BFF, and the BFF signs the user in with an external identity provider and calls the server API for the user.

## Language

### Authentication

**BFF session**:
The server-side record of one signed-in browser at the BFF. It holds the user's tokens, and the browser holds only a cookie that points to it.
_Avoid_: Cookie session, user session, login

**IdP session**:
The user's signed-in state at the identity provider. One IdP session can back many BFF sessions, and it can end without the browser present.
_Avoid_: SSO session, provider session

**Identity provider**:
The external OpenID Connect server that authenticates the user, for example Keycloak.
The `AuthProvider` setting selects it.
_Avoid_: OP, IdP server

### Concurrency

**Version**:
The Postgres `xmin` of the row of an aggregate root. It is opaque, so compare it only for equality. A change to an owned child also gives the root a new version.
_Avoid_: Row version, ETag, revision

**Stale version**:
A change that the caller based on a version that is no longer current. The caller loads the record again and repeats the change.
_Avoid_: Concurrency conflict, already exists

### Agents

**First-party client**:
The OAuth client of this app, which the BFF and Swagger UI use. A user who signs in through it acts with all of their own permissions.
_Avoid_: App client, main client

**Agent**:
An OAuth client other than the first-party client that a tenant lets act in the tenant. It is either a delegated agent or a machine agent.
_Avoid_: Bot, integration, app

**Delegated agent**:
An agent that acts for a signed-in user of its tenant. It holds only the permissions that both the user and its grant have.
_Avoid_: Assistant, on-behalf-of client

**Machine agent**:
An agent that acts for itself in one tenant, with no user. It holds its grant. Only the operator binds a client to a tenant as a machine agent.
_Avoid_: Service account, machine client

**Grant**:
The permissions that a tenant gives an agent. It is the most that the agent can hold. Nobody can grant a permission that they do not hold.
_Avoid_: Scope, consent

**All-tenants client**:
An OAuth client that the operator trusts to read, or to manage, the data of all tenants. It acts for itself, with no user.
_Avoid_: Admin client, super client

**Unknown client**:
An OAuth client that is not the first-party client, not an all-tenants client, and not an agent for the token. It sees no data and holds no permission.
_Avoid_: Unregistered agent, guest

### Decisions

**Case record**:
A record that holds each fact that one rule set reads, filled by one loader for each caller of the rules.
_Avoid_: Context, snapshot, request model

**Refusal**:
The answer of a rule that blocks a change. It is one kind of a closed set, with the request field and the message, and the handler maps each kind to an exception.
_Avoid_: Error code, failure reason

**Proof type**:
A type that only the decision makes, which the write method takes, so the write cannot run without the check.
_Avoid_: Token, ticket, witness
