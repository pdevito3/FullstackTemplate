# Spec: the home-rolled BFF

This spec replaces `Duende.BFF` and `Duende.BFF.Yarp` in `FullstackTemplate.Bff` with our own code. It is the hand-off of the map [Migrate the BFF off Duende BFF](https://github.com/pdevito3/FullstackTemplate/issues/6). Each decision in this spec links to the ticket that holds its reasons.

The terms **BFF session**, **IdP session**, and **Identity provider** have the meanings in [`CONTEXT.md`](../../CONTEXT.md).

The ADRs for this effort:

- [0003: Replace Duende BFF with our own BFF](../adr/0003-replace-duende-bff.md)
- [0004: Server-side BFF sessions in `bffdb`](../adr/0004-server-side-bff-sessions.md)
- [0005: Provider features by discovery](../adr/0005-provider-features-by-discovery.md)
- [0006: Our own DPoP proof validation on the server](../adr/0006-own-dpop-proof-validation.md)

## Contents

1. [Destination and scope](#1-destination-and-scope)
2. [The wire contract](#2-the-wire-contract)
3. [Phase 1 design, by concern](#3-phase-1-design-by-concern)
4. [The parity checklist](#4-the-parity-checklist)
5. [The compliance matrix](#5-the-compliance-matrix)
6. [Provider and seed changes](#6-provider-and-seed-changes)
7. [The test plan and CI](#7-the-test-plan-and-ci)
8. [Repo and doc changes](#8-repo-and-doc-changes)
9. [Phase 2: DPoP](#9-phase-2-dpop)
10. [The build order](#10-the-build-order)

## 1. Destination and scope

### Goal

The BFF has no package with a license cost. It keeps full parity with the current BFF for each behavior that we use. It complies with OAuth 2.1 and the OpenID Connect relying-party specs.

### Packages

- Remove `Duende.BFF` 4.0.1 and `Duende.BFF.Yarp` 4.0.1 from `Directory.Packages.props` and `FullstackTemplate.Bff.csproj`.
- Keep the Microsoft `OpenIdConnect` and `Cookies` handlers.
- Keep YARP (`Yarp.ReverseProxy`). Add it as a direct package reference, because today it comes in through `Duende.BFF.Yarp`.
- Keep `Duende.AccessTokenManagement.OpenIdConnect` 4.1.0 (Apache 2.0). Add it as a direct package reference, because today it comes in through `Duende.BFF`. Its dependency `Duende.IdentityModel` (Apache 2.0) stays with it.
- Add `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` to `Directory.Packages.props`. The Npgsql EF Core provider and the Aspire Npgsql EF Core integration already have a version there. The BFF project references all three for `bffdb`.

### The compliance set

| Short name | Spec |
|---|---|
| OAuth 2.1 | [draft-ietf-oauth-v2-1](https://datatracker.ietf.org/doc/draft-ietf-oauth-v2-1/) |
| Core | [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html), for the RP |
| RFC 9207 | [OAuth 2.0 Authorization Server Issuer Identification](https://www.rfc-editor.org/rfc/rfc9207) |
| RPIL | [OpenID Connect RP-Initiated Logout 1.0](https://openid.net/specs/openid-connect-rpinitiated-1_0.html) |
| RFC 7009 | [OAuth 2.0 Token Revocation](https://www.rfc-editor.org/rfc/rfc7009) |
| BCL | [OpenID Connect Back-Channel Logout 1.0](https://openid.net/specs/openid-connect-backchannel-1_0.html), as a receiver |
| BBA | [OAuth 2.0 for Browser-Based Applications](https://datatracker.ietf.org/doc/draft-ietf-oauth-browser-based-apps/), the BFF section |
| RFC 9126 | [OAuth 2.0 Pushed Authorization Requests](https://www.rfc-editor.org/rfc/rfc9126) |
| RFC 9449 | [OAuth 2.0 DPoP](https://www.rfc-editor.org/rfc/rfc9449), phase 2 only |

"Compliant" is a property of the BFF. The BFF obeys each rule of the set whenever the identity provider offers the feature, and it never uses an unsafe fallback ([ADR 0005](../adr/0005-provider-features-by-discovery.md)).

### Standing decisions

- The wire contract for the browser does not change (section 2). The frontend code does not change. Only its product copy changes (section 8).
- The code goes in folders in `FullstackTemplate.Bff`, one folder for each concern. There is no separate library.
- The cutover replaces Duende in one change. There is no side-by-side flag.
- DPoP is phase 2 (section 9). PAR is in phase 1.
- `DuendeDemo` stays the default auth provider of the template.

### Out of scope

- A change of the default auth provider.
- A new wire contract for the browser.
- Our own OIDC client code in place of the Microsoft `OpenIdConnect` handler.
- `private_key_jwt` client authentication. It stays possible later through `IClientAssertionService` of `Duende.AccessTokenManagement`.
- SPA handling of a 401 from `/api`.
- An official OpenID certification ([Certification or CI report](https://github.com/pdevito3/FullstackTemplate/issues/12)).

### Sources

The behavior of the current BFF is in [the Duende BFF inventory](https://github.com/pdevito3/FullstackTemplate/blob/research/duende-bff-implicit-behavior/docs/research/duende-bff-implicit-behavior.md). The gaps of the Microsoft handler are in [the OIDC handler gap analysis](https://github.com/pdevito3/FullstackTemplate/blob/research/oidc-handler-gap-analysis/docs/research/oidc-handler-gap-analysis.md). The features of each provider are in [the provider capability matrix](https://github.com/pdevito3/FullstackTemplate/blob/research/provider-capability-matrix/docs/research/provider-capability-matrix.md). The conformance suite is in [the conformance suite research](https://github.com/pdevito3/FullstackTemplate/blob/research/openid-conformance-suite-rp/docs/research/openid-conformance-suite-rp.md).

## 2. The wire contract

The browser sees exactly these endpoints, headers, claims, and status codes. Each item is the same as today, unless the item says "change".

### Endpoints

| Path | Method | `X-CSRF` | Behavior |
|---|---|---|---|
| `/bff/login` | any | no | Starts a login. See [Login](#login). |
| `/bff/logout` | GET | no | Ends the BFF session and starts RP-initiated logout. See [Logout](#logout). |
| `/bff/user` | GET | yes | Returns the claims of the BFF session. See [`/bff/user`](#bffuser). |
| `/bff/backchannel` | POST | no | The back-channel logout receiver. Only the identity provider calls it. |
| `/signin-oidc` | GET | no | The OIDC callback (Microsoft handler). Change: a callback that is not a GET gets a 400. |
| `/signout-callback-oidc` | GET | no | The return from the `end_session_endpoint` (Microsoft handler). |
| `/local-api/hello` | GET | yes | The sample local API. |
| `/api/{**catch-all}` | any | yes | The proxy to `<server>/api/...`. `/api` itself also matches. Route matching is case-insensitive. |
| `/health`, `/alive` | GET | no | The service-defaults health checks. |
| `/` and the SPA fallback | GET | no | A redirect to the Vite URL in development. The SPA from `wwwroot` otherwise. |

These paths go away on purpose ([Duende BFF features to drop](https://github.com/pdevito3/FullstackTemplate/issues/22)): `/bff/silent-login`, `/bff/silent-login-callback`, `/bff/diagnostics`, and the front-channel `/signout-oidc`. A request to one of these paths gets the SPA fallback or a 404, like any unknown path.

### `/bff/login`

- Query `returnUrl` (optional). It must pass the [local URL rule](#the-local-url-rule). If it fails, the response is 400 `application/problem+json` with the title "Invalid return url". The default is `PathBase`, or `/`.
- Query `prompt` (optional). It must be in `prompt_values_supported` of discovery. If it is not, the response is 400 `application/problem+json` with the title "Invalid prompt value".
- If the handler has no discovery document, the response is 404.
- Otherwise the response is a 302 to the identity provider.
- If the identity provider returns an `error` for a login with a `prompt` (for example `login_required`), the callback sends a 302 to the `returnUrl`. The request does not fail.

### `/bff/logout`

- Query `sid`. If the BFF session has a `sid` claim, the query must have the same value. If not, the response is 400 `application/problem+json` with the title "Invalid Session ID". An anonymous user, or a BFF session with no `sid` claim, needs no `sid`.
- Query `returnUrl` (optional). It must pass the local URL rule, or the response is 400 problem. The default is `/`.
- The response is a 302 to the `end_session_endpoint`, or to the local `returnUrl` if the identity provider has no `end_session_endpoint`.

### `/bff/user`

- Without `X-CSRF: 1`, the response is 401 with an empty body.
- Anonymous: 401 with an empty body.
- Signed in: 200 `application/json`. The body is an array of `{ "type": string, "value": string, "valueType": null }`. The array has these items, in this order:
  1. Each claim of the BFF session principal, in the stored order.
  2. `bff:logout_url`, if the principal does not have it: `PathBase + /bff/logout`, plus `?sid=<url-encoded sid>` if a `sid` claim exists.
  3. `bff:session_expires_in`: the seconds until the BFF session expires, rounded to whole seconds, as a JSON number. The value is the earlier of the sliding expiry and the absolute limit. Change: the absolute limit is new (section 3.1).
  4. `bff:session_state`, if the authorize response had `session_state`.
- Query `slide=false`: the request reads the BFF session but does not renew the sliding expiry.

### The `X-CSRF` header

- The header name is `X-CSRF`. The value must be exactly `1`. Only the first value counts.
- The check applies to every HTTP method, GET too, and to signed-in and anonymous users.
- On failure: 401, an empty body, and no `WWW-Authenticate`. The request ends there.

### Status codes on API endpoints

On `/bff/user`, `/local-api/*`, and `/api/*`, a 302 challenge becomes a 401, and a 302 forbid becomes a 403. The BFF removes `Location` and every `Set-Cookie` header from that response. The login and logout endpoints keep their 302.

### Cookies

| Cookie | Attributes |
|---|---|
| `__Host-bff` (the BFF session) | `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/`, no `Domain`. The value is the Data Protection form of the BFF session key. Change: `SameSite` was `Lax`, `Secure` followed the request, and the value held the whole ticket in chunks. |
| `.AspNetCore.Correlation.*` and `.AspNetCore.OpenIdConnect.Nonce.*` | `HttpOnly`, `Secure`, `SameSite=Lax`. The BFF sets `Lax`. The Microsoft default is `None`. |

### Headers from the API

The API response passes through the proxy as today. Change: the proxy removes `Set-Cookie` from the API response (section 3.5).

## 3. Phase 1 design, by concern

Each subsection is one folder in `FullstackTemplate.Bff`. The configuration keys in this section are the names to use.

### 3.1 BFF sessions and the store

Decision: [Session storage](https://github.com/pdevito3/FullstackTemplate/issues/11). ADR: [0004](../adr/0004-server-side-bff-sessions.md).

**The store seam.** One interface for the BFF session store. It can create, read, renew, and delete a BFF session, and find BFF sessions by `sub` and by `sid`. The cookie handler uses it through its `SessionStore` (`ITicketStore`). It has two adapters:

- EF Core on Postgres, for production. It has its own `DbContext` and its own migrations.
- In memory, for tests only.

**The database.** A new database `bffdb` on the same Postgres server as `appdb`. The AppHost adds it with `AddDatabase("bffdb")` and gives it to the BFF with `WithReference` and `WaitFor`. An `--isolated` run gets an empty `bffdb`. The BFF applies its migrations at startup.

**The row.** One row for each BFF session:

| Column | Content |
|---|---|
| `Key` | A random 256-bit opaque id, the primary key. The cookie holds it through Data Protection. |
| `SubjectId` | The `sub` claim, with an index. |
| `SessionId` | The `sid` claim (null if none), with an index. |
| `Created`, `Renewed` | UTC times. |
| `Expires` | The sliding expiry. |
| `AbsoluteExpires` | `Created` plus the absolute limit. It never moves. |
| `Ticket` | The serialized authentication ticket with the tokens, encrypted with Data Protection. |
| `xmin` | The Postgres concurrency token. |

**Lifetime.**

- `Bff:Session:SlidingExpiration`, default 14 days (parity). The cookie handler renews it on a request after half of it is gone (parity).
- `Bff:Session:AbsoluteLifetime`, default 30 days. This is new. A renewal never moves `Expires` past `AbsoluteExpires`.
- Each read rejects a row with `Expires` or `AbsoluteExpires` in the past.

**Conditional writes.** These rules correct the fault in which a late refresh brought a BFF session back ([Test id_token retention](https://github.com/pdevito3/FullstackTemplate/issues/23)):

- A read keeps the `xmin` of the row for the rest of the request.
- A renew writes the ticket only if the row still exists and its `xmin` is the same. A renew after a successful write updates the `xmin` that the request holds.
- Logout and every other end of a BFF session delete the row first.
- If a renew finds no row, it does not create one. If the refresh token in the ticket is not the one that the request read at its start, a refresh ran in this request. The BFF then revokes that new refresh token.
- A refresh writes inside the refresh lock, with the `xmin` of the row that the lock read (section 3.7). Thus the new tokens always reach the row.
- A renew that changes only the expiry never replaces newer tokens. If it loses the `xmin` race, it reads the row again and applies only its expiry change.
- A renew never moves `Renewed` or `Expires` back. It writes the later of its own value and the value in the row that it read, also after it loses the `xmin` race.
- A renew tries at most 3 times. After 3 lost races, it logs the failure and stops. A token write that stops this way is dropped, and the BFF revokes its new refresh token, as for a missing row.
- A new login always gets a new key. In `OnTicketReceived`, if a BFF session exists, the BFF deletes its row, deletes the cookie and all its chunks in the browser, and signs out the cookie before the new sign-in. Thus a renew never puts the ticket of a new login, with a new `sub` or `sid`, on an old key.
  - The BFF revokes the refresh token of the deleted row, as logout does. If the new login has the same `sid`, the BFF keeps the old token: with Keycloak, a revoke of one refresh token ends the client session, and thus also the new tokens. The end of the IdP session then ends the old token.
  - The BFF finds the old BFF session only through the cookie of the callback. With a cross-site identity provider, the browser does not send the `SameSite=Strict` cookie on the redirect to `/signin-oidc`. The old row then stays until it expires or the cleanup deletes it, and the BFF does not revoke its refresh token.
- The store adds to `bff.sessions.started` when it creates a row.

**The `id_token` on a refresh.** If a refresh response has no `id_token`, the store keeps the stored `id_token` in the renewed ticket. `Duende.AccessTokenManagement` writes `null` in that case. Keycloak, FusionAuth, Authentik, and the Duende Demo send a new `id_token` today, so this rule is defensive ([Test id_token retention](https://github.com/pdevito3/FullstackTemplate/issues/23)).

**Data Protection.** The keys persist in the same `DbContext` through `PersistKeysToDbContext`, with one application name for all BFF instances. A deploy then does not log out every user. Encryption of the keys at rest is a deployment decision.

**Cleanup.** A hosted service in each instance deletes the expired rows in batches. Each batch comes in the order of `Expires` or of `AbsoluteExpires`, from the index on that column, so the batch query stops at the batch size. `Bff:Session:CleanupInterval`, default 10 minutes. The delete is idempotent, so it needs no lock. Each delete adds to the `bff.sessions.ended` count with `reason=expiry`.

**Scale.** The design is for many BFF instances. The BFF session store, the Data Protection keys, the refresh lock, and the replay cache of the logout tokens are all in `bffdb`. A Redis or `HybridCache` adapter can come later behind the same seam.

### 3.2 Login and the callback checks

Decisions: [Client authentication method](https://github.com/pdevito3/FullstackTemplate/issues/17), [PAR in phase 1](https://github.com/pdevito3/FullstackTemplate/issues/13), [Behavior when a provider lacks a feature](https://github.com/pdevito3/FullstackTemplate/issues/18), [Forwarded host trust](https://github.com/pdevito3/FullstackTemplate/issues/16).

**Schemes.** Register the cookie and OIDC handlers with `AddCookie` and `AddOpenIdConnect`. The cookie scheme is the default for authenticate and forbid. The OIDC scheme is the default for challenge and sign-out (parity).

**OIDC handler options.**

| Option | Value |
|---|---|
| `Authority`, `ClientId`, `ClientSecret` | From `Auth:Authority`, `Auth:ClientId`, `Auth:ClientSecret` (parity) |
| `ResponseType` | `code` |
| `ResponseMode` | `query` |
| `UsePkce` | `true` (`S256`) |
| `GetClaimsFromUserInfoEndpoint` | `true` |
| `SaveTokens` | `true` |
| `MapInboundClaims` | `false` |
| `Scope` | `openid profile email offline_access`, plus `Auth:Audience` if `Auth:UseAudienceAsScope` is `true` (parity) |
| `TokenValidationParameters` | `NameClaimType` and `RoleClaimType` from configuration (parity) |
| `RequireHttpsMetadata` | `Auth:RequireHttpsMetadata`, default `true` outside Development (parity) |
| `PushedAuthorizationBehavior` | From `Auth:Features:PushedAuthorization` (below) |
| `RemoteSignOutPath` | Empty, so that the handler maps no `/signout-oidc` ([Front-channel logout endpoint](https://github.com/pdevito3/FullstackTemplate/issues/14)) |
| `CallbackPath`, `SignedOutCallbackPath` | The defaults, `/signin-oidc` and `/signout-callback-oidc` |
| `CorrelationCookie.SameSite`, `NonceCookie.SameSite` | `Lax` (the Microsoft default is `None`) |

Keep the two trace events of `LoginTraceLink` (`OnRedirectToIdentityProvider` and `OnTicketReceived`).

**The public origin.** `Bff:PublicOrigin` is required. The BFF builds `redirect_uri` and `post_logout_redirect_uri` from this origin, never from the request.

- A middleware after `UseForwardedHeaders` sets `Request.Scheme` and `Request.Host` from `Bff:PublicOrigin`. The handler then builds both URIs from the public origin.
- Do not rewrite `redirect_uri` in `OnRedirectToIdentityProvider`. The handler stores the value before the event runs, and it sends the stored value on the code exchange.

- In development, the AppHost gives the BFF its own `https` URL. The Vite proxy changes the origin, so the providers already register the BFF URL.
- In a deployment, the deployment config gives the public URL of the BFF.

**The `prompt` parameter.** `/bff/login` stores `prompt` in the authentication properties. `OnRedirectToIdentityProvider` copies it to the authorize request (parity).

**The method guard.** A middleware before `UseAuthentication` returns a 400 for a request to `CallbackPath` that is not a GET. This makes `response_mode=query` strict. The guard cannot be in `OnMessageReceived`, because the handler fails a POST with no form before that event, and a failure there gives a 500.

**The callback checks in `OnMessageReceived`.** These run before the correlation check and before the handler reads `error` or `code`. `OnRemoteFailure` maps a failure of these checks to a 400 problem response.

1. **RFC 9207 `iss` check** (section 2.4), with `Auth:Features:IssuerParameter`:
   - If `iss` is present, it must equal the discovery `issuer` with ordinal compare. If not, fail.
   - If `iss` is missing and discovery has `authorization_response_iss_parameter_supported: true`, fail.
   - If `iss` is missing and discovery does not list it, accept the callback (the Authentik case).
   - `Require` has two effects. The BFF stops at startup if discovery does not list the field as `true`, and a callback with no `iss` fails.
   - `Disable`: no check.
   - The check also covers an error response. The BFF does not trust an error response that fails the check.
2. **The prompt error.** If a login with a `prompt` gets an `error`, send a 302 to the `returnUrl` and stop (parity).

**The strict audience check in `OnTokenValidated`.** The `aud` of the ID token must hold only the client id. Do not set `ProtocolValidator.IdTokenValidator`, because a custom delegate turns off the default checks.

**Client authentication.** `Auth:ClientAuthentication` has the values `Auto` (default), `Basic`, and `Post`.

- `Auto` uses `client_secret_basic` if `token_endpoint_auth_methods_supported` lists it, or if the field is missing.
- If the field lists only `client_secret_post`, `Auto` uses `client_secret_post` and logs one startup warning.
- The code exchange uses a hook in `OnAuthorizationCodeReceived`. The event gives only the token request message, not the HTTP request. Thus the hook redeems the code itself:
  - It sends the token request through `context.Backchannel`, so that the phase 2 `AuthorizationServerDPoPHandler` still applies.
  - It sends `Authorization: Basic`. It form-encodes the client id and the secret before the Base64 step (RFC 6749 section 2.3.1).
  - It calls `context.HandleCodeRedemption` with the response.
- The Microsoft default is `client_secret_post`.
- Refresh and revocation use the same method through `ClientCredentialStyle` of `Duende.AccessTokenManagement`. Its default is already `AuthorizationHeader`.
- The PAR request uses the same method (RFC 9126 section 2). The handler always sends `client_secret_post` to the PAR endpoint, so for `client_secret_basic` a hook in `OnPushAuthorization` pushes the request itself through the backchannel and calls `context.HandlePush`.

**The signing keys.** The OIDC handler owns the `ConfigurationManager` for discovery and the signing keys. The back-channel receiver uses the same instance. `RefreshOnIssuerKeyNotFound` stays `true`, so a new key causes a refresh. `Auth:MetadataRefreshInterval` sets only `RefreshInterval`, the shortest time between two refreshes (minimum 1 s). `AutomaticRefreshInterval` keeps the handler default, because its minimum is 5 minutes. The default of `Auth:MetadataRefreshInterval` is the handler default. Only the conformance job sets a short value (section 7).

### 3.3 Provider features and settings

Decision: [Behavior when a provider lacks a feature](https://github.com/pdevito3/FullstackTemplate/issues/18). ADR: [0005](../adr/0005-provider-features-by-discovery.md).

Each feature has a three-state setting under `Auth:Features`. The values are `Auto`, `Require`, and `Disable`.

| Key | Discovery field | Default | `Auto` behavior when the field is missing |
|---|---|---|---|
| `Revocation` | `revocation_endpoint` | `Auto` | Skip the revocation. |
| `IssuerParameter` | `authorization_response_iss_parameter_supported` | `Auto` | Accept a callback with no `iss`. A present `iss` must still match. |
| `BackChannelLogout` | `backchannel_logout_supported` | `Auto` | The endpoint stays mapped. It validates each token. |
| `EndSession` | `end_session_endpoint` | `Auto` | Logout sends the browser to the local `returnUrl`. |
| `PushedAuthorization` | `pushed_authorization_request_endpoint` | `Auto` | Use the plain authorize URL. |
| `DPoP` | `dpop_signing_alg_values_supported` | `Disable` | Phase 2 (section 9). |

- `Auto` uses the feature if discovery lists it.
- `Require` stops the BFF at startup if discovery does not list it.
- `Disable` never uses the feature.
- `PushedAuthorization` maps to `PushedAuthorizationBehavior`: `Auto` to `UseIfAvailable`, `Require` to `Require`, and `Disable` to `Disable`.
- At startup, the BFF reads discovery one time. It decides the active features from this snapshot, and logs one warning for each feature that is missing under `Auto`. A change of the provider features needs a restart of the BFF. The endpoint URLs still come from the live `ConfigurationManager`.
- If the discovery read fails at startup, the BFF stops with an error. The AppHost already waits for the provider.
- `Auth:Features:Revocation` replaces `Auth:RevokeRefreshTokenOnLogout`.

`WithBffAuth` in `AuthProviders/AuthProviderConfig.cs` passes these settings to the BFF. The `RevokeRefreshTokenOnLogout` property of `AuthProviderConfig` goes away. The capability table of the e2e tests (section 7) holds the expected features of each provider.

### 3.4 Logout and back-channel logout

Decisions: [Session storage](https://github.com/pdevito3/FullstackTemplate/issues/11), [Behavior when a provider lacks a feature](https://github.com/pdevito3/FullstackTemplate/issues/18), [Front-channel logout endpoint](https://github.com/pdevito3/FullstackTemplate/issues/14).

#### The local URL rule

A `returnUrl` is local if it is `/`, or if it starts with `/` and its second character is not `/` or `\`. It must have no ASCII control character. Absolute URLs and `//host` fail (parity). `/bff/login` and `/bff/logout` use this rule.

#### Logout

`/bff/logout` does these steps in this order:

1. Authenticate the cookie. Do the `sid` check (section 2).
2. Do the local URL check on `returnUrl`.
3. Delete the BFF session row. Add to `bff.sessions.ended` with `reason=logout`.
4. If revocation is active, revoke the refresh token of the deleted row through `Duende.AccessTokenManagement` (RFC 7009, `token_type_hint=refresh_token`). The delete returns the row as it was at the delete. Thus, if a refresh in another request wrote new tokens after step 1, the BFF revokes the new refresh token. If no row was deleted, another end of the BFF session came first, and the BFF revokes nothing. If the identity provider returns an error, log it and continue. If revocation is not active, skip it with no exception.
5. Sign out the cookie scheme.
6. If `end_session_endpoint` is active, sign out the OIDC scheme. The handler sends the browser there with `id_token_hint` and `post_logout_redirect_uri=<public origin>/signout-callback-oidc`. After the return, it sends the browser to `returnUrl`.
7. If not, send a 302 to `returnUrl`.

#### Back-channel logout

`POST /bff/backchannel` is always mapped. The identity provider calls it with a form field `logout_token`.

1. Set `Cache-Control: no-cache, no-store` and `Pragma: no-cache` on each response.
2. If `logout_token` is missing, return 400.
3. Validate the JWT with `JsonWebTokenHandler` and the `ConfigurationManager` of the OIDC handler:
   - `RequireSignedTokens = true`. The `alg` must not be `none`.
   - `iss` equals the discovery `issuer`. `aud` holds the client id.
   - `iat` is present. `exp` is in the future, with the default clock skew.
   - If `typ` is present, it must be `logout+jwt` (BCL 2.4 and 4.1). A token with no `typ` passes, because BCL does not require it. The e2e tests prove that the real Keycloak and Authentik tokens pass.
4. Check the claims:
   - `sub` or `sid` is present.
   - `events` has the member `http://schemas.openid.net/event/backchannel-logout`.
   - `nonce` is absent.
5. Check `jti` for a replay. BCL 2.4 requires `jti`, so a token with no `jti` returns 400. The replay cache is a table in `bffdb`, keyed by `jti`, with the token `exp` as its expiry. The BFF writes the `jti` before it ends a session. A replay returns 400. A failed write of the `jti` returns 400 and ends no session, so that the provider can send the token again. The cleanup service also deletes the expired rows of this table.
6. End the BFF sessions. The request abort of the client does not cancel this step.
   - If the token has a `sid`, delete the rows with that `sid`.
   - If it has only a `sub`, delete all the rows of that `sub`.
   - The BFF does not revoke the refresh tokens of the deleted rows. The provider already ended its session. With Keycloak, a revoke of one refresh token also ends the other sessions of the user for the client, and Keycloak sends no logout token for them, so their BFF sessions would stay signed in until the next refresh fails.
   - If a row delete fails, the BFF removes the `jti` and returns 500. The provider can then send the same token again.
   - Add to `bff.sessions.ended` with `reason=backchannel_logout`, one for each row.
7. Return 200. Any failure in steps 2 to 5 returns 400.

If `Auth:Features:BackChannelLogout` is `Require`, the BFF stops at startup when `backchannel_logout_supported` is missing or `false`.

#### Front-channel logout

Off. `RemoteSignOutPath` is empty. Only back-channel logout tells the BFF that an IdP session ended.

### 3.5 The CSRF check and the status code rewrite

Decision: [Inventory the implicit behavior of Duende BFF v4](https://github.com/pdevito3/FullstackTemplate/issues/7).

- **Endpoint metadata.** A marker for BFF API endpoints. An `AsBffApiEndpoint()` extension adds it. `/bff/user`, `/local-api/hello`, and the `/api` route have it.
- **The `X-CSRF` middleware.** It runs after `UseRouting` and before `UseAuthorization`, because it reads the endpoint metadata. It applies the rules of section 2 to each endpoint that has the marker.
- **The 302 rewrite.** A decorator of `IAuthenticationService`. After a challenge or a forbid on an endpoint with the marker, a 302 becomes a 401 or a 403. The decorator removes `Location` and every `Set-Cookie` header.

### 3.6 The proxy

Decision: [Proxy header rules](https://github.com/pdevito3/FullstackTemplate/issues/21).

YARP `MapForwarder("/api/{**catch-all}", "<server>/api", ...)`. The server URL comes from service discovery (`services:server:https:0`, then `http:0`), as today.

**Request transforms:**

- Remove `Cookie`.
- Remove `Authorization` and `DPoP`, always. The BFF is the only source of tokens. Change: today a browser `Authorization` header goes through.
- Strip the `/api` prefix. The destination has `/api`, so `/api/v1/x` goes to `<server>/api/v1/x`.
- Copy the other request headers, `X-CSRF` too.
- Keep the YARP `X-Forwarded-*` defaults: `Set` for `-For`, `-Proto`, `-Host`, and `-Prefix`, and remove `Forwarded`.
- Attach the token (section 3.7).

**Response transforms:**

- Remove the `Set-Cookie` values of the API response. Change: a `Set-Cookie: __Host-bff=...` from the API could replace the session cookie. The API sets no cookies today.
- A `Set-Cookie` that the BFF itself wrote in this request (a renewed BFF session) stays. YARP appends the API headers to the headers that the BFF already wrote, so a plain `ResponseHeaderRemove("Set-Cookie")` removes both. The transform must filter only the values from the proxied response.

**The forwarder.** Keep the hop-by-hop rules, the 100 s activity timeout, and the 502 or 504 error mapping. The forwarder has no retries, because a retry in a proxy can send a POST two times.

### 3.7 Token management and the refresh lock

Decisions: [Refresh lock across BFF instances](https://github.com/pdevito3/FullstackTemplate/issues/24), [Behavior when a token refresh fails](https://github.com/pdevito3/FullstackTemplate/issues/20).

**Registration.** Call `AddOpenIdConnectAccessTokenManagement` one time, after the OIDC options. `RefreshBeforeExpiration` comes from `Auth:RefreshBeforeExpirationSeconds`, default 120 (parity). If a token response has no `expires_in`, token management treats the access token as one that does not expire, and never refreshes it. `Auth:DefaultAccessTokenLifetime` gives such a token a lifetime, at the code exchange and at each refresh. It has no default. Only the conformance job sets it (section 7). `IUserTokenStore` keeps its default, `AuthenticationSessionUserAccessTokenStore`. Its sign-in writes through the BFF session store.

**The token attach.** A YARP request transform gets the user token through `GetUserAccessTokenAsync`:

| Case | Result |
|---|---|
| Anonymous | Forward with no token. |
| Signed in, the token is valid or the refresh worked | `Authorization: Bearer <token>`. The token type compare is case-insensitive. |
| Refresh failed with `invalid_grant`, or its ID token failed the checks below (permanent) | End the BFF session: delete the row, add to `bff.sessions.ended` with `reason=refresh_failure`, and sign out the cookie. Then forward with no token. |
| Refresh failed for a temporary cause (network error, timeout, 5xx, a token request timeout, or a lock timeout) | Keep the BFF session. If the current access token is still valid, send it. If not, forward with no token. |
| Token type `DPoP` | The transform reads the token type of the stored token in phase 1. With `Auth:Features:DPoP` at `Disable`, a `DPoP` token cannot occur. If one occurs, the transform logs an error and forwards with no token. Phase 2 adds the DPoP branch (section 9). |

**The ID token of a refresh response.** OIDC Core 12.2. `RefreshIdTokenValidation` wraps the token endpoint of token management, inside the other wrappers. A refresh response with no `id_token` passes, and the BFF keeps the stored `id_token`. A refresh response with an `id_token` passes only if the token passes these checks:

- The signature, with the signing keys of discovery and the algorithm of the client registration. An unknown `kid` refreshes the keys one time, as at the login.
- `iss` equals the discovery `issuer`.
- `aud` holds only the client id, as at the login. If `azp` is present, it equals the client id.
- `exp` is in the future, and `iat` is present. A nonce is not required.
- `sub` equals the `sub` of the BFF session.
- If the BFF session and the token both have an `auth_time`, the two values are equal.

If a check fails, the refresh fails with the error `bff_invalid_refresh_id_token`. Token management then does not store the new tokens, and the request does not use them. The failure is permanent: the refresh lock deletes the row, and the BFF session ends as for `invalid_grant`. A wrong `iss` or `sub` is a sign of a wrong or attacked identity provider, so a retry with the same provider must not continue the BFF session. The BFF also revokes the refresh token of the rejected response, when revocation is active, because no BFF session holds it. The log gives the reason and a SHA-256 fingerprint of the ID token, never a token value.

The checks and the revocation do not use the cancellation of `Bff:Refresh:TokenRequestTimeout`, because that cancellation gives a temporary failure and keeps the BFF session. Each gets its own limit of `Bff:Refresh:TokenRequestTimeout` instead. If the signing keys do not load within the limit, the check fails. A revocation that fails or reaches its limit is logged, and the refresh still fails with `bff_invalid_refresh_id_token`.

Each failed refresh adds to `bff.refresh.failures` with `kind=permanent` or `kind=temporary`. After a permanent failure, the next `/bff/user` gives a 401, so the SPA shows the user as signed out. The SPA does not change. The refresh has no retry inside the call. The next request that needs a token tries again. A retry inside the call can send a rotated refresh token two times, after the identity provider used it but the response got lost, and then the session ends. A retry also holds the refresh lock and its `bffdb` connection longer. The refresh goes through the backchannel `HttpClient` of the OIDC handler, which does not come from `IHttpClientFactory`, so the standard resilience handler of the service defaults does not apply to it.

**The refresh lock.** Our own `IUserTokenRequestConcurrencyControl` replaces the default. It has two layers:

1. **In process.** Keep the merge by refresh token, so that one instance takes the database lock only one time for each refresh. The first request runs the refresh. Each other request waits at most `Bff:Refresh:LockTimeout` plus `Bff:Refresh:TokenRequestTimeout` plus 2 s, and stops its wait when its own client goes away. The first request can wait that long for the lock and then for the identity provider, so a shorter wait can end before the refresh ends. A wait that reaches the limit is a temporary failure.
2. **Across instances.** In a `bffdb` transaction, take `pg_advisory_xact_lock` with a 64-bit hash of the BFF session key. Then read the BFF session row again.
   - If the stored refresh token is not the one that the caller has, return the stored tokens. Do not call the identity provider.
   - If it is the same, run the refresh. The delegate of `ExecuteWithConcurrencyControlAsync` runs `RefreshAccessTokenAsync` and then `StoreTokenAsync`, so the lock wraps the refresh and the write. The write uses the `xmin` of the row that the lock read. Thus a waiter always reads the new tokens.
   - The BFF session store and the lock use the same scoped `DbContext` and the same connection, so that the write is inside the lock transaction.

The lock releases itself when the transaction ends, also if the instance stops. It does not block a plain read of the row.

**The wait.** `Bff:Refresh:LockTimeout`, default 10 s. If the wait reaches the limit, the BFF treats it as a temporary failure.

**The token request.** The lock transaction holds a `bffdb` connection during the call to the identity provider, so a hung identity provider must not hold it long. `Bff:Refresh:TokenRequestTimeout`, default 5 s, limits the refresh call. It must be less than `Bff:Refresh:LockTimeout`, so that a request that waits for the lock gets the result of the refresh. A call that reaches the limit is a temporary failure.

**Cancellation.** The shared refresh and its commit do not stop when a client goes away. The identity provider can have rotated the refresh token already. A rollback then leaves the used refresh token in the row, and the next refresh gets `invalid_grant`. Thus the token attach does not give `RequestAborted` to token management, and the token write ignores it. The request that runs the refresh waits for its end, because the refresh uses the scoped `DbContext` of that request.

### 3.8 Cookies and host trust

Decisions: [Session cookie SameSite](https://github.com/pdevito3/FullstackTemplate/issues/15), [Forwarded host trust](https://github.com/pdevito3/FullstackTemplate/issues/16).

**The session cookie.** `__Host-bff`, `HttpOnly`, `SecurePolicy=Always`, `SameSite=Strict`, `Path=/`, and no `Domain`.

- `SecurePolicy=Always` also works in development, because browsers treat `http://localhost` as a secure context.
- The SPA works with `Strict`, because each call that needs the BFF session is same-site. The HTML of the first load after the redirect from the identity provider needs no cookie.
- Remove the comment "Lax is required for OIDC redirects" in `AuthExtensions.cs`. That rule applies only to the correlation and nonce cookies.
- The cookie holds only the key. Cookie chunking does not occur.

**The correlation and nonce cookies.** The BFF sets `SameSite=Lax`, so that the browser sends them on the top-level GET to `/signin-oidc`. Today they have the Microsoft default `None`.

**Forwarded headers.**

- `ForwardedHeaders = XForwardedFor | XForwardedProto`. Stop reading `X-Forwarded-Host`.
- Call `UseForwardedHeaders` one time, first in the pipeline. Duende's second call goes away.

**The host filter.** `AllowedHosts` holds the host of `Bff:PublicOrigin`, plus the hosts in `Bff:AdditionalAllowedHosts`. A request with another `Host` header gets a 400.

- The identity provider containers and the conformance suite call `/bff/backchannel` at `host.docker.internal`. The AppHost adds this host in a local run only, not when it publishes. The conformance job adds it too.
- The health probes call the BFF at its internal host. The AppHost adds `localhost` in a local run only. A deployment adds the host of its probes.

**CORS.** No change. The default policy allows the Vite origin with credentials.

### 3.9 Telemetry

Decision: [Duende BFF features to drop](https://github.com/pdevito3/FullstackTemplate/issues/22).

The `Duende.Bff` meter goes away. New counters go on the existing `FullstackTemplate.Bff` meter in `Telemetry.cs`, and obey `docs/telemetry.md`:

| Counter | Unit | Tags |
|---|---|---|
| `bff.sessions.started` | `{session}` | none |
| `bff.sessions.ended` | `{session}` | `reason`: `logout`, `backchannel_logout`, `refresh_failure`, or `expiry` |
| `bff.refresh.failures` | `{failure}` | `kind`: `permanent` or `temporary` |

Each startup warning of section 3.3 is one structured log with the feature name. The `Duende` log level overrides in `appsettings.json` and `appsettings.Development.json` change to the new namespaces.

### 3.10 The pipeline

`Program.cs` has this order:

1. `UseForwardedHeaders`
2. The public origin middleware
3. Serilog request logging
4. `UseExceptionHandler`
5. `UseStaticFiles`, only when there is no Vite URL
6. `UseCors`
7. The method guard for `/signin-oidc`
8. `UseAuthentication`
9. `UseRouting`
10. The `X-CSRF` middleware
11. `UseAuthorization`
12. The endpoints: `/bff/*`, `/local-api/hello`, the `/api` forwarder, the health checks, and `/` or the SPA fallback.

There is no startup filter. Each step is visible in `Program.cs`.

## 4. The parity checklist

Each behavior of the current BFF, from [the inventory](https://github.com/pdevito3/FullstackTemplate/blob/research/duende-bff-implicit-behavior/docs/research/duende-bff-implicit-behavior.md) and the later tickets.

| # | Behavior today | Result | Reason | Section |
|---|---|---|---|---|
| 1 | `/bff/login`: local `returnUrl`, 400 problem, default `/` | Kept | Open-redirect defense | 2, 3.4 |
| 2 | `/bff/login`: `prompt` pass-through, `prompt_values_supported` check | Kept | [#22](https://github.com/pdevito3/FullstackTemplate/issues/22) | 2, 3.2 |
| 3 | `/bff/login`: 302 to `returnUrl` on an IdP error for a prompt login | Kept | Parity | 3.2 |
| 4 | `/bff/login`: 404 without discovery | Kept | Parity | 2 |
| 5 | `/bff/logout`: `sid` match, 400 "Invalid Session ID" | Kept | Logout CSRF defense | 2 |
| 6 | `/bff/logout`: local `returnUrl` | Kept | Open-redirect defense | 3.4 |
| 7 | `/bff/logout`: refresh token revocation | Changed | `Auth:Features:Revocation` replaces `RevokeRefreshTokenOnLogout`. A missing endpoint is safe. [#18](https://github.com/pdevito3/FullstackTemplate/issues/18) | 3.3, 3.4 |
| 8 | `/bff/logout`: RP-initiated logout with `id_token_hint` | Kept | A missing `end_session_endpoint` now falls back to the local `returnUrl` | 3.4 |
| 9 | `/bff/user`: `X-CSRF`, 401 empty body when anonymous | Kept | Wire contract | 2 |
| 10 | `/bff/user`: claim array, `bff:logout_url`, `bff:session_state` | Kept | Wire contract | 2 |
| 11 | `/bff/user`: `bff:session_expires_in` as a number | Changed | It now also reflects the absolute limit | 2, 3.1 |
| 12 | `/bff/user?slide=false` | Kept | [#22](https://github.com/pdevito3/FullstackTemplate/issues/22) | 2 |
| 13 | `X-CSRF: 1` on every BFF API endpoint and method, 401 empty body | Kept | CSRF defense | 2, 3.5 |
| 14 | 302 to 401 and 403, `Location` and `Set-Cookie` removed | Kept | Wire contract | 2, 3.5 |
| 15 | Cookie name `__Host-bff`, `HttpOnly`, 14 days sliding | Kept | Wire contract | 3.1, 3.8 |
| 16 | Cookie `SameSite=Lax` | Changed | `Strict`, the BBA SHOULD. [#15](https://github.com/pdevito3/FullstackTemplate/issues/15) | 3.8 |
| 17 | Cookie `SecurePolicy=SameAsRequest` | Changed | `Always`. [#15](https://github.com/pdevito3/FullstackTemplate/issues/15) | 3.8 |
| 18 | Tokens in the cookie ticket, with chunking | Changed | Server-side BFF sessions. [#11](https://github.com/pdevito3/FullstackTemplate/issues/11) | 3.1 |
| 19 | A refresh restarts the 14 days | Kept | A refresh renews the row, up to the absolute limit | 3.1 |
| 20 | Default schemes: cookie for authenticate and forbid, OIDC for challenge and sign-out | Kept | Parity | 3.2 |
| 21 | Early handling of `/signin-oidc` and `/signout-callback-oidc` by a startup filter | Changed | A plain `UseAuthentication` gives the same result | 3.10 |
| 22 | Proxy `/api/{**catch-all}`, `Cookie` removed, `/api` stripped | Kept | Parity | 3.6 |
| 23 | Proxy copies a browser `Authorization` header | Changed | Removed, with `DPoP`. [#21](https://github.com/pdevito3/FullstackTemplate/issues/21) | 3.6 |
| 24 | Proxy copies `Set-Cookie` from the API | Changed | Removed. [#21](https://github.com/pdevito3/FullstackTemplate/issues/21) | 3.6 |
| 25 | Proxy `X-Forwarded-*`, hop-by-hop, 100 s timeout, 502 and 504 | Kept | Parity | 3.6 |
| 26 | Token attach with `UserOrNone` | Kept | Anonymous still forwards with no token | 3.7 |
| 27 | A failed refresh keeps the session | Changed | `invalid_grant` ends the BFF session. [#20](https://github.com/pdevito3/FullstackTemplate/issues/20) | 3.7 |
| 28 | Refreshed cookie kept in the proxied response | Kept | Parity | 3.6 |
| 29 | `AddOpenIdConnectAccessTokenManagement` called two times | Changed | One call | 3.7 |
| 30 | `RefreshBeforeExpiration` from configuration | Kept | Parity | 3.7 |
| 31 | In-process refresh merge | Kept | With an advisory lock across instances. [#24](https://github.com/pdevito3/FullstackTemplate/issues/24) | 3.7 |
| 32 | ATM writes `null` to `id_token` when a refresh has none | Changed | The store keeps the old `id_token`. [#23](https://github.com/pdevito3/FullstackTemplate/issues/23) | 3.1 |
| 33 | `client_secret_post` on the code exchange | Changed | `client_secret_basic`. [#17](https://github.com/pdevito3/FullstackTemplate/issues/17) | 3.2 |
| 34 | PAR when discovery lists it | Kept | Now under `Auth:Features:PushedAuthorization`. [#13](https://github.com/pdevito3/FullstackTemplate/issues/13) | 3.3 |
| 35 | `/bff/backchannel` returns 200 and ends no session | Changed | It ends BFF sessions, with `typ`, `iat`, and `jti` checks | 3.4 |
| 36 | Front-channel `/signout-oidc` | Dropped | [#14](https://github.com/pdevito3/FullstackTemplate/issues/14) | 3.4 |
| 37 | `/bff/silent-login`, `/bff/silent-login-callback` | Dropped | Obsolete. Nothing uses them. [#22](https://github.com/pdevito3/FullstackTemplate/issues/22) | 2 |
| 38 | `/bff/diagnostics` | Dropped | It shows raw tokens with no CSRF check | 2 |
| 39 | License check and the hourly diagnostics log | Dropped | No license | none |
| 40 | Multi-frontend machinery, `AddHybridCache` | Dropped | Not used | none |
| 41 | Two `UseForwardedHeaders` calls, `X-Forwarded-Host` trusted | Changed | One call, no `X-Forwarded-Host`, a fixed public origin. [#16](https://github.com/pdevito3/FullstackTemplate/issues/16) | 3.8 |
| 42 | `Duende.Bff` meter (always zero) | Changed | Our own counters. [#22](https://github.com/pdevito3/FullstackTemplate/issues/22) | 3.9 |
| 43 | `/local-api/hello` with `AsBffApiEndpoint` | Kept | Parity | 2, 3.5 |
| 44 | Management error responses as `HttpValidationProblemDetails` with status 400 | Kept | Wire contract | 2 |
| 45 | Data Protection keys only in the process | Changed | Keys in `bffdb`. A deploy does not log out every user. | 3.1 |

## 5. The compliance matrix

Each rule, where the design meets it, and the test that proves it. "Default" means that a Microsoft handler default meets the rule, and a test pins it.

| Rule | Spec section | Design | Test |
|---|---|---|---|
| Authorization code flow only | OAuth 2.1 4.1 | `ResponseType=code` | Unit: the authorize URL has `response_type=code`. Conformance: basic plan. |
| Response mode is the one that we asked for | OAuth 2.1 4.1.2 | Method guard | Integration: a POST to `/signin-oidc` gets a 400, also with no form body. |
| PKCE `S256` | OAuth 2.1 4.1.1, 7.5.2 | `UsePkce=true` | Unit: the authorize URL has `code_challenge_method=S256`. Conformance. |
| `state` bound to the browser, one-time use | OAuth 2.1 2.3.3, 7.10, Core 3.1.2.1 | Default | Conformance: basic plan. |
| `nonce` | Core 3.1.2.1, 3.1.3.7 | Default | Conformance: basic plan. |
| ID token `iss`, `aud`, `azp`, `exp`, `iat`, `sub` | Core 3.1.3.7 | Default | Conformance: basic plan. |
| ID token has no untrusted audience | Core 3.1.3.7 | Strict audience check | Unit: an ID token with an extra `aud` fails. Conformance. |
| Signing key rollover | Core 10.1.1 | Shared `ConfigurationManager`, `RefreshOnIssuerKeyNotFound` | Conformance: config plan, with `Auth:MetadataRefreshInterval` of 1 s. |
| Userinfo `sub` matches the ID token | Core 5.3.2 | Default | Conformance: basic plan. |
| Exact redirect URI | OAuth 2.1 2.3.1 | `Bff:PublicOrigin` | Integration: a forged `X-Forwarded-Host` still gives the configured `redirect_uri`. |
| Client authentication | OAuth 2.1 2.4.1, RFC 6749 2.3.1 | `client_secret_basic`, discovery fallback | Unit: each `Auth:ClientAuthentication` case. Conformance: basic plan. |
| `iss` response parameter | RFC 9207 2.4 | `iss` check | Unit: each case of section 3.2. Conformance: basic plan. |
| PAR when the AS offers it | RFC 9126 | `Auth:Features:PushedAuthorization` | Unit: the setting map. e2e: the authorize URL has only `client_id` and `request_uri` on Keycloak and the Duende Demo. |
| Refresh token rotation and reuse | OAuth 2.1 4.3.3 | The refresh lock | Integration: two hosts, one refresh call. e2e: the provider rejects a reused refresh token, and the BFF refreshes on each API call with the rotated token, on Keycloak, FusionAuth, and Authentik. Conformance: refresh plan. |
| ID token of a refresh response: signature, `iss`, `aud`, `azp`, `exp`, `iat`, same `sub` and `auth_time` | Core 12.2 | `RefreshIdTokenValidation`, section 3.7 | Integration: a wrong `iss`, `sub`, `aud`, or `auth_time`, or a bad signature, ends the BFF session. A valid ID token or no ID token gives the new tokens. Conformance: refresh plan. |
| Refresh token revocation | RFC 7009 2.1 | Logout step 4 | Integration: a fake token endpoint gets the revocation. e2e: the revoked token fails on Keycloak and the Duende Demo. |
| RP-initiated logout with `id_token_hint` and `post_logout_redirect_uri` | RPIL 2, 3 | Logout step 6 | e2e on each provider. Conformance: RP-initiated logout plan. |
| Back-channel logout endpoint and token validation | BCL 2.4, 2.5, 2.6 | Back-channel logout | Unit: each check of section 3.4. Conformance: back-channel plan. |
| Back-channel logout ends the sessions | BCL 2.7 | Delete by `sid` or `sub` | Integration: the rows go away, and no revocation request reaches the provider. e2e on Keycloak and Authentik. |
| Back-channel response codes and `no-store` | BCL 2.8 | Steps 1 and 7 | Unit. Conformance: back-channel plan. |
| Cross-JWT confusion | BCL 2.4, 4.1 | `typ` check | Unit: an ID token sent as a logout token fails. |
| Cookie `__Host-` prefix, `HttpOnly`, `Secure`, `SameSite=Strict` | BBA 6.1.3.2 | Section 3.8 | e2e: the three cookie tests. Integration: the `Set-Cookie` attributes. |
| CSRF defense | BBA 6.1.3.3 | `X-CSRF` check, `SameSite=Strict` | Integration: each BFF API endpoint and method without the header gets a 401. |
| No tokens in the browser | BBA 6.1 | Server-side sessions, header rules | Integration: no response body or header holds a token. The proxy tests in section 7. |
| Local return URLs | OAuth 2.1 7.13.1 | The local URL rule | Unit: each case of the rule. |

## 6. Provider and seed changes

Decision: [Provider seed and version changes](https://github.com/pdevito3/FullstackTemplate/issues/19).

- **Version policy.** Each provider image gets an exact patch tag in its provider class in `FullstackTemplate.AppHost/AuthProviders/<Name>/`, as the Cognito and ZeroFactor classes do. Today the tags float: Keycloak `26.0`, Authentik `2024.12`, and FusionAuth `latest`. The research found 26.0.8, 2024.12.5, and 1.69.4 behind them.
- **Authentik.** Upgrade from 2024.12.5 to the latest stable 2026.8.x. Read the release notes between the two versions for changes to the worker and the Redis setup.
- **Authentik logout flow.** The seed in `FullstackTemplate.AppHost/AuthProviders/Authentik/seed/default.yaml` uses an invalidation flow with a user logout stage. RP-initiated logout then also ends the IdP session.
- **Back-channel URLs.** Register `<BFF URL>/bff/backchannel` in the Keycloak and Authentik seeds. The container must reach the BFF, for example through `host.docker.internal`. Find out how Aspire exposes the BFF endpoint to a container. FusionAuth and the Duende Demo cannot take part. Back-channel logout in Authentik is a preview feature since 2025.8, so the seed can need to turn it on.
- **Keycloak password grant.** Set `directAccessGrantsEnabled` to `false`. Nothing in the repo uses it.
- **Refresh token rotation.** Turn on one-time use in Keycloak (`revokeRefreshToken`, max reuse `0`) and in FusionAuth.
- **Feature settings.** `WithBffAuth` in `AuthProviders/AuthProviderConfig.cs` gives `Auth__Features__*` to the BFF. The defaults (`Auto`) are correct for each provider.
- **Public origin.** `WithBffAuth` gives `Bff__PublicOrigin` from the BFF `https` endpoint.
- **`bffdb`.** The AppHost adds the database and gives it to the BFF.
- **The conformance provider.** The folder `AuthProviders/Conformance/` gets a `ConformanceAuthProvider` for the conformance job. Its authority is the suite, and its `Bff:PublicOrigin` is the BFF `https` URL. The suite client config registers `<BFF https URL>/signin-oidc`, `/signout-callback-oidc`, and `/bff/backchannel` at `host.docker.internal`.

The capability table (section 7) holds the expected result of these changes:

| Feature | Keycloak | FusionAuth | Authentik 2026.8 | Cognito | ZeroFactor | Duende Demo |
|---|---|---|---|---|---|---|
| Revocation | yes | no | yes | no | yes | yes |
| `iss` parameter | yes | yes | no | no | yes | yes |
| Back-channel logout (testable) | yes | no | yes | no | no | no (cannot reach the BFF) |
| `end_session_endpoint` | yes | yes | yes | no | yes | yes |
| PAR | yes | no | no | no | no | yes |
| Refresh rotation | yes | yes | yes | no | no (stateless refresh tokens) | no |
| DPoP (phase 2) | `--features=dpop` | Enterprise plan only | skipped (added in 2026.8, not tested) | not tested | not tested | yes |

The live discovery test (section 7) makes sure that the table is correct.

## 7. The test plan and CI

Decisions: [Test plan in CI](https://github.com/pdevito3/FullstackTemplate/issues/26), [OpenID conformance suite for an RP](https://github.com/pdevito3/FullstackTemplate/issues/10), [Certification or CI report](https://github.com/pdevito3/FullstackTemplate/issues/12).

### Test layers

- **BFF unit and integration tests.** A new project `tests/FullstackTemplate.Bff.Tests`. The integration tests use a `WebApplicationFactory` for the BFF, a fake identity provider, a fake API that echoes the headers, and a Postgres container for `bffdb`. The refresh lock tests run two BFF hosts on one `bffdb`. The tests of sections 3 and 5 go here.
- **e2e tests.** The Playwright tests in `frontend/e2e`, against each provider of the `e2e` matrix.
  - A fixed capability table in the test project holds the features of each provider (section 6).
  - A feature test runs only for the providers that have the feature. For the other providers, the test is skipped and gives the reason.
  - For a missing feature, a test proves the fallback, for example that logout still ends the BFF session.
  - One test makes sure that live discovery matches the table.
  - The cookie tests: a full login that ends signed in on the first load, a deep link from another origin for a signed-in user, and a cross-site top-level GET to `/api` that gets a 401.
  - A GET to `/signout-oidc` does not end the BFF session.
- **Conformance.** The OpenID Foundation suite from `docker-compose-prebuilt.yml`, with our own Playwright driver for `/bff/login` and `/bff/logout`. It runs these five plans:
  - `oidcc-client-basic-certification-test-plan`
  - `oidcc-client-config-certification-test-plan`
  - `oidcc-client-rp-initiated-logout-rp-basic`
  - `oidcc-client-back-channel-logout-rp-basic`
  - `oidcc-client-refreshtoken-test-plan`

### Tests from the decision tickets

These tests come from the tickets. Each one is in addition to the tests of section 5.

| Layer | Test | Ticket |
|---|---|---|
| e2e | Keycloak and the Duende Demo: the authorize URL has only `client_id` and `request_uri`, and the login finishes. FusionAuth and Authentik: the login uses the plain authorize URL. | [#13](https://github.com/pdevito3/FullstackTemplate/issues/13) |
| Unit | Each `Auth:Features:PushedAuthorization` value maps to the correct `PushedAuthorizationBehavior`. | [#13](https://github.com/pdevito3/FullstackTemplate/issues/13) |
| Integration | A forged `X-Forwarded-Host` on `/bff/login` still gives the configured `redirect_uri`. | [#16](https://github.com/pdevito3/FullstackTemplate/issues/16) |
| Integration | A `Host` that is not on the list gets a 400. | [#16](https://github.com/pdevito3/FullstackTemplate/issues/16) |
| Integration | `invalid_grant`: the BFF session row is gone, the request goes with no token, and `/bff/user` returns 401. | [#20](https://github.com/pdevito3/FullstackTemplate/issues/20) |
| Integration | A 503 while the token is still valid: the request goes with the current token, and the BFF session stays. | [#20](https://github.com/pdevito3/FullstackTemplate/issues/20) |
| Integration | A 503 after the token expired: the request goes with no token, the BFF session stays, and the next call tries the refresh again. | [#20](https://github.com/pdevito3/FullstackTemplate/issues/20) |
| Integration | A browser `Authorization` header never reaches the API, for an anonymous request too. | [#21](https://github.com/pdevito3/FullstackTemplate/issues/21) |
| Integration | A signed-in request reaches the API with only the BFF token. | [#21](https://github.com/pdevito3/FullstackTemplate/issues/21) |
| Integration | `Cookie` never reaches the API. | [#21](https://github.com/pdevito3/FullstackTemplate/issues/21) |
| Integration | A `Set-Cookie` from the API never reaches the browser, and the renewed BFF cookie still does. | [#21](https://github.com/pdevito3/FullstackTemplate/issues/21) |
| Integration | Two BFF hosts on one `bffdb`, a fake token endpoint with rotation: concurrent calls on both hosts make exactly one refresh call, and every call gets the new token. | [#24](https://github.com/pdevito3/FullstackTemplate/issues/24) |
| Integration | A lock wait that reaches the limit gives the temporary-failure path. | [#24](https://github.com/pdevito3/FullstackTemplate/issues/24) |
| e2e | The providers with rotation (Keycloak, FusionAuth, and Authentik): two `/api` calls at the same time after a forced refresh both succeed, and the BFF session still works after them. | [#24](https://github.com/pdevito3/FullstackTemplate/issues/24) |
| Integration | A late refresh after logout does not bring the BFF session back, and the BFF revokes its new refresh token. | [#11](https://github.com/pdevito3/FullstackTemplate/issues/11) |
| Integration | A new login on a browser with a BFF session gets a new key. | This spec |
| Unit | Each `Auth:ClientAuthentication` case of section 3.2. | [#17](https://github.com/pdevito3/FullstackTemplate/issues/17) |

### CI jobs

- **No nightly run.** The BFF jobs run only on a BFF change, and on `workflow_dispatch`.
- **The `changes` job.** A path filter. The BFF jobs depend on it. Do not use `paths:` on the trigger, because a skipped job then reports no status. These paths count as a BFF change in `ci.yml` and in `conformance.yml`:
  - `FullstackTemplate.Bff/**`
  - `FullstackTemplate.AppHost/**`
  - `FullstackTemplate.Server/Resources/Entrypoints/**`, the token check of the Server
  - `Directory.Packages.props`, `Directory.Build.props`, `global.json`, and `dotnet-tools.json`
  - `frontend/package.json` and `frontend/pnpm-lock.yaml`
  - The workflow file
- `ci.yml` also counts these paths:
  - `tests/FullstackTemplate.Bff.Tests/**`
  - `frontend/e2e/**` and `frontend/playwright.config.ts`
  - `frontend/vite.config.ts` and `frontend/src/**`, the SPA and the dev server proxy that the e2e tests drive
- `conformance.yml` also counts `frontend/conformance/**`.
- **`bff-tests`.** The BFF unit and integration tests, with the two-host lock tests.
- **`e2e`.** A matrix of eight legs: Keycloak, Keycloak with DPoP, FusionAuth, Authentik, Cognito, ZeroFactor, the Duende Demo, and the Duende Demo with DPoP. Each leg runs `dotnet aspire start --isolated` and waits for healthy resources. The two Duende Demo legs use `continue-on-error`, because the Duende Demo is a public service. A failure of one of the other six legs fails the workflow.
  - Each leg of a provider in the stack sets `BffRefreshBeforeExpirationSeconds` to 86400, so that each API call refreshes, and `EXPECT_REFRESH_MARGIN=true`. The two Duende Demo legs set no margin and `EXPECT_REFRESH_MARGIN=false`.
  - A DPoP leg sets `DPoP=true` and `EXPECT_DPOP=true`. The other legs set `EXPECT_DPOP=false`.
  - `run-against-stack.ts` fails the leg when an `EXPECT_*` variable and the stack disagree, so a leg cannot pass while its refresh tests or its DPoP test skip.
- **`conformance`.** The job runs `dotnet aspire start --isolated` with the `Conformance` provider. The suite starts with `docker-compose-prebuilt.yml` and an override file with `extra_hosts: host.docker.internal:host-gateway`. The BFF trusts the self-signed suite certificate. The job sets `Auth:MetadataRefreshInterval` to 1 s, so that each test module gets fresh signing keys.
  - The suite sends no `expires_in`, so the job sets `Auth:DefaultAccessTokenLifetime` to 1 s. Then the next API call refreshes the token.
  - The access tokens of the suite are opaque. So the server, with `Auth:AccessTokenValidation=Userinfo` (Development only), checks each token at the userinfo endpoint of the suite. The happy path module of the refresh plan finishes on that userinfo call with the refreshed token. The driver stops the API calls of the SPA, because a call after a module finished fails it.
  - Each `FAILED` result fails the job.
  - Each `WARNING` or `REVIEW` result fails the job, unless it is in a committed list of accepted results. Each entry in the list gives a reason.
  - Each module that does not finish, in each plan, is "not run" and fails the job. A plan with no result must not give a green job.
  - Each plan blocks, the refresh plan too. Its negative modules send an ID token with a wrong `iss` or `sub` in the refresh response (section 3.7).
- **Reports.** Each job uploads the exported suite results, the Playwright traces and screenshots, and the stack logs as artifacts, kept for 30 days. The job summary lists each plan with its pass, warning, and fail counts.
- **Scripts.** The scripts for the stacks and the suite go in the repo, so that a developer can run the same commands locally.

## 8. Repo and doc changes

| File | Change |
|---|---|
| `Directory.Packages.props` | Remove `Duende.BFF` and `Duende.BFF.Yarp`. Add `Yarp.ReverseProxy`, `Duende.AccessTokenManagement.OpenIdConnect`, and the EF Core and Data Protection packages. |
| `FullstackTemplate.Bff/FullstackTemplate.Bff.csproj` | The same package changes. |
| `FullstackTemplate.Bff/appsettings*.json` | Replace the `Duende` log level overrides. Add the new keys with their defaults. |
| `FullstackTemplate.AppHost/AppHost.cs` | Add `bffdb`. |
| `FullstackTemplate.AppHost/AuthProviders/AuthProviderConfig.cs` | Remove `RevokeRefreshTokenOnLogout`. Add the feature settings, `Bff__PublicOrigin`, and `Bff__AdditionalAllowedHosts`. |
| `FullstackTemplate.AppHost/AuthProviders/Conformance/` | The new `ConformanceAuthProvider`. It sets `RunsE2eTests` to false, so it needs no file in `frontend/e2e/idps/`. The suite logs in with no login form. |
| `FullstackTemplate.AppHost/Config.cs` | The provider image tags. |
| `README.md` | Remove "using Duende BFF". Change the project tree line to "BFF proxy (YARP, OIDC)". |
| `AGENTS.md` | The `FullstackTemplate.Bff` row: "BFF proxy (YARP and OIDC auth)". |
| `docs/authentication.md` | The new login and logout flows, the BFF sessions in `bffdb`, the feature settings with a table of the active features for each provider, the back-channel logout, and how a team certifies its own deployment with the same conformance plans. |
| `docs/telemetry.md` | The BFF now has custom metrics (section 3.9). |
| `.agents/skills/**` | No change. The skills name only the Duende demo IdP. |
| `frontend/src/routes/index.tsx` | The subtitle ".NET Aspire + React with Duende BFF" becomes ".NET Aspire + React with a BFF". |
| `frontend/src/routes/about.tsx` | "Duende BFF" becomes "BFF (YARP and OIDC)". |
| `FullstackTemplate.Server/Services/CurrentUserService.cs` | No change. Its comments name the Duende Demo IdP, not Duende BFF. |

## 9. Phase 2: DPoP

Decision: [DPoP in phase 2](https://github.com/pdevito3/FullstackTemplate/issues/25). ADR: [0006](../adr/0006-own-dpop-proof-validation.md).

Phase 1 needs no change for phase 2. The BFF session row does not change.

- **The key.** One DPoP key for the BFF client, from a secret in the configuration (`DPoPJsonWebKey` of `Duende.AccessTokenManagement`). All instances share it. A key rotation is a change of the configuration. A key for each BFF session can come later.
- **The code exchange.** `Duende.AccessTokenManagement` binds it through its `AuthorizationServerDPoPHandler`: `dpop_jkt` on the authorize request, and a proof on the code exchange. The Microsoft handler needs no DPoP support.
- **The userinfo call.** Token management keeps the key of the code exchange in `HttpContext.Items`, so its handler would add a proof without `ath` to the userinfo call. `UserInfoDPoP` removes the key after the code exchange. For a DPoP-bound token, it sends the userinfo call with `Authorization: DPoP` and a proof with `ath`, and it retries one time after `use_dpop_nonce`. When the provider answers with only a Bearer challenge, as Duende IdentityServer does at userinfo, it sends the call one more time as Bearer.
- **The setting.** `Auth:Features:DPoP`, with `Disable` as the default. DPoP needs a configured key, so `Auto` is not a safe default.
- **The proxy.** The token transform reads the token type of the stored token.
  - `Bearer` gives `Authorization: Bearer`.
  - `DPoP` gives `Authorization: DPoP` and a new `DPoP` proof for each request, through `IDPoPProofService`.
  - A `use_dpop_nonce` error from the API gets one retry with the nonce.
- **The server.** Our own code on top of the Microsoft JWT bearer handler, for the RFC 9449 section 4.3 rules:
  - `typ=dpop+jwt`, an asymmetric `alg`, and the signature with the header `jwk`
  - `htm` and `htu`
  - an `iat` window
  - `ath`
  - `cnf.jkt` of the access token
  - no replay of `jti`. The replay cache is a table in `appdb`, so that all server instances share it. A cleanup deletes the expired rows.
- **Not used.** `Duende.AspNetCore.Authentication.JwtBearer`, because it has the commercial Duende license.
- **Proof.** The Duende Demo, and Keycloak with `--features=dpop` (behind the pinned tag). FusionAuth, Authentik, Cognito, and ZeroFactor are not tested with DPoP, as the capability table shows.

## 10. The build order

Each slice is one pull request that you can test alone. Slice 1 must reach parity before it merges, because the cutover replaces Duende in one change.

1. **Cutover at parity.**
   - Remove the Duende packages. Add the Microsoft OIDC and cookie handlers, the YARP forwarder with the token attach, `/bff/login`, `/bff/user`, `/bff/logout`, `/bff/backchannel` (validation only), the `X-CSRF` check, and the 302 rewrite.
   - Keep the cookie ticket, `SameSite=Lax`, and the other current behaviors for this slice.
   - Start `tests/FullstackTemplate.Bff.Tests`.
   - Done when the current Keycloak e2e tests pass with no change to the frontend.
2. **BFF sessions.** `bffdb`, the store seam and its two adapters, the Data Protection keys, the absolute limit, the conditional writes, the `id_token` rule, and the cleanup.
3. **Back-channel logout and the session metrics.** End BFF sessions by `sid` or `sub`, the `typ`, `iat`, and `jti` checks, and the three counters.
4. **Login hardening and the provider features.**
   - The `iss` check and `client_secret_basic`.
   - The strict audience check, the method guard, and the PAR setting.
   - The `Auth:Features` settings and the startup warnings.
   - Front-channel logout off.
5. **Refresh.** The advisory lock and the refresh failure rules.
6. **The proxy header rules, `SameSite=Strict`, and host trust.**
7. **Provider seed and version changes.**
8. **CI.** The `changes` job, the e2e matrix of the providers, and the conformance suite.
9. **Template and doc changes.**
10. **Phase 2: DPoP.**
