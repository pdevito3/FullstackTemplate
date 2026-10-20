# Provider capability matrix

Research for issue #9 of the map #6 "Migrate the BFF off Duende BFF". Date of research: 2026-10-20.

## Question

What does each auth provider of the template support? The features are RFC 7009 revocation, Back-Channel Logout 1.0, Front-Channel Logout 1.0, RP-Initiated Logout 1.0, the `sid` claim, RFC 9207 `iss`, PAR (RFC 9126), DPoP (RFC 9449), refresh token rotation, and `S256` PKCE.

## Answer in short

- Only the Duende Demo and Keycloak support the full phase 1 compliance set.
- FusionAuth has no RFC 7009 revocation and no Back-Channel Logout.
- authentik 2024.12 has no Back-Channel Logout, no RFC 9207 `iss`, and no full RP-Initiated Logout (it ignores `post_logout_redirect_uri`).
- PAR: Duende Demo and Keycloak only. DPoP: Duende Demo only, by default. Keycloak has DPoP as a preview feature that is off. FusionAuth has DPoP on the Enterprise plan only.
- No seed config registers a back-channel logout URL. The Duende Demo cannot call a local BFF in any case.

## Versions in use

The versions come from `FullstackTemplate.AppHost/AuthProviders.cs`.

| Provider | Image or authority | Resolved version | Evidence |
|---|---|---|---|
| Duende Demo | `https://demo.duendesoftware.com` | Duende IdentityServer 8.0.9, demo source commit `ca5710d1` | The home page shows "v8.0.9". The response header `x-revision: ca5710d1` matches the head of [DuendeSoftware/demo.duendesoftware.com](https://github.com/DuendeSoftware/demo.duendesoftware.com), whose csproj references `Duende.IdentityServer` 8.0.9. |
| Keycloak | `quay.io/keycloak/keycloak:26.0`, `start-dev`, no `--features` | 26.0.8 | Startup log "Keycloak 26.0.8 on JVM" and `/admin/serverinfo` `systemInfo.version = 26.0.8`. |
| FusionAuth | `fusionauth/fusionauth-app:latest` | 1.69.4 | `latest` and `1.69.4` have the same digest `sha256:830efe2c...`, pushed 2026-10-14 ([Docker Hub tags](https://hub.docker.com/v2/repositories/fusionauth/fusionauth-app/tags?page_size=10)). |
| authentik | `ghcr.io/goauthentik/server:2024.12` | 2024.12.5 (last 2024.12.x patch) | [GitHub releases](https://github.com/goauthentik/authentik/releases). The image digest was not inspected. |

Note: `latest` for FusionAuth is not a pinned version. The matrix for FusionAuth changes when FusionAuth ships a new release.

## Method

- Duende Demo: the live discovery document of `https://demo.duendesoftware.com/.well-known/openid-configuration`, the demo source, the IdentityServer source at tag `is-8.0.9`, and the Duende docs.
- Keycloak: a live `start-dev` container (discovery document and token tests with `admin-cli`), the 26.0.8 docs, and the source at tag `26.0.8`.
- FusionAuth: a live 1.69.4 container (discovery document and endpoint probes), the FusionAuth docs, and the release notes.
- authentik: the source at tag `version/2024.12.5` (the discovery document is built in `views/provider.py`), the docs, and the release notes. No container was started.

All test containers were removed after the tests.

## Source keys

The cells of the matrix use these keys.

| Key | Source |
|---|---|
| D-DISC | Live discovery document, https://demo.duendesoftware.com/.well-known/openid-configuration (fetched 2026-10-20) |
| D-CFG | Demo client `interactive.confidential`, https://github.com/DuendeSoftware/demo.duendesoftware.com/blob/main/src/Config.cs |
| D-CLIENT | Duende client model reference, https://docs.duendesoftware.com/identityserver/reference/models/client/ |
| D-OPT | Duende options reference, https://docs.duendesoftware.com/identityserver/reference/options/ |
| D-TOK | https://github.com/DuendeSoftware/products/blob/is-8.0.9/identity-server/src/IdentityServer/Services/Default/DefaultTokenService.cs (lines 128-132) |
| K-DISC | Live discovery document of Keycloak 26.0.8 (`/realms/master/.well-known/openid-configuration`) |
| K-ADMIN | https://www.keycloak.org/docs/26.0.8/server_admin/index.html |
| K-EP | https://github.com/keycloak/keycloak/blob/26.0.8/docs/guides/securing-apps/partials/oidc/available-endpoints.adoc |
| K-WK | https://github.com/keycloak/keycloak/blob/26.0.8/services/src/main/java/org/keycloak/protocol/oidc/OIDCWellKnownProvider.java |
| K-REP | https://github.com/keycloak/keycloak/blob/26.0.8/core/src/main/java/org/keycloak/protocol/oidc/representations/OIDCConfigurationRepresentation.java |
| K-ATTR | https://github.com/keycloak/keycloak/blob/26.0.8/server-spi-private/src/main/java/org/keycloak/protocol/oidc/OIDCConfigAttributes.java |
| K-PROF | https://github.com/keycloak/keycloak/blob/26.0.8/common/src/main/java/org/keycloak/common/Profile.java |
| K-TM | https://github.com/keycloak/keycloak/blob/26.0.8/services/src/main/java/org/keycloak/protocol/oidc/TokenManager.java |
| K-UP25 | https://github.com/keycloak/keycloak/blob/26.0.8/docs/documentation/upgrading/topics/changes/changes-25_0_0.adoc |
| F-DISC | Live discovery document of FusionAuth 1.69.4 (`/.well-known/openid-configuration`) |
| F-LOGOUT | https://fusionauth.io/docs/apis/oauth/logout |
| F-OAUTH | https://fusionauth.io/docs/apis/oauth |
| F-REVOKE | https://fusionauth.io/docs/apis/jwt/revoke-refresh-tokens |
| F-APP | https://fusionauth.io/docs/apis/applications/create-an-application |
| F-TENANT | https://fusionauth.io/docs/apis/tenants/create-a-tenant |
| F-TOKENS | https://fusionauth.io/docs/lifecycle/authenticate-users/oauth/tokens |
| F-ISS | https://fusionauth.io/docs/lifecycle/authenticate-users/oauth/issuer-validation |
| F-DPOP | https://fusionauth.io/docs/lifecycle/authenticate-users/oauth/dpop |
| F-AUTHZ | https://fusionauth.io/docs/apis/oauth/authorize |
| F-RN | https://fusionauth.io/docs/release-notes/ |
| A-SRC | `https://github.com/goauthentik/authentik/blob/version/2024.12.5/authentik/providers/oauth2/` plus the file name in the cell |
| A-RN-2025.8 | https://docs.goauthentik.io/releases/2025.8 |
| A-RN-2025.10 | https://docs.goauthentik.io/releases/2025.10 |
| A-RN-2026.5 | https://docs.goauthentik.io/releases/2026.5 |
| A-RN-2026.8 | https://docs.goauthentik.io/releases/2026.8 |

## Matrix

"Yes" means that the provider supports the feature at the version in use, with no license and no feature flag. Each cell gives the discovery key where one exists, and the source.

| Feature | Duende Demo (IdentityServer 8.0.9) | Keycloak 26.0.8 | FusionAuth 1.69.4 | authentik 2024.12.5 |
|---|---|---|---|---|
| RFC 7009 revocation | **Yes.** `revocation_endpoint` = `/connect/revocation`. [D-DISC] | **Yes.** `revocation_endpoint` = `/protocol/openid-connect/revoke`. Revokes access and refresh tokens. Live test: a revoked refresh token gave `invalid_grant`. [K-DISC, K-EP, K-WK] | **No.** No `revocation_endpoint` in discovery. `POST /oauth2/revoke` gave 404. The only revocation is the proprietary `DELETE /api/jwt/refresh`, which needs an API key. [F-DISC, F-OAUTH, F-REVOKE] | **Yes.** `revocation_endpoint` = `/application/o/revoke/`. Deletes the access or refresh token. It does not check that the token belongs to the calling client. [A-SRC `views/token_revoke.py`, `views/provider.py`] |
| Back-Channel Logout 1.0 | **Yes.** `backchannel_logout_supported: true`, `backchannel_logout_session_supported: true`. [D-DISC, D-CLIENT `BackChannelLogoutUri`] | **Yes.** `backchannel_logout_supported: true`, `backchannel_logout_session_supported: true`. Client attribute `backchannel.logout.url`. [K-DISC, K-WK, K-ATTR] | **No.** `backchannel_logout_supported: false`. The docs do not mention it. [F-DISC, F-RN] | **No.** No discovery key. It came in 2025.8.0 as Preview. [A-SRC `views/provider.py`, A-RN-2025.8] |
| Front-Channel Logout 1.0 | **Yes.** `frontchannel_logout_supported: true`, `frontchannel_logout_session_supported: true`. [D-DISC, D-CLIENT `FrontChannelLogoutUri`] | **Yes.** `frontchannel_logout_supported: true`, `frontchannel_logout_session_supported: true`. Iframe based. [K-DISC, K-REP, K-ADMIN] | **Partial.** `frontchannel_logout_supported: true`, no `frontchannel_logout_session_supported`. Iframes to each `logoutURL` when `logoutBehavior` is `AllApplications`. No documented `iss` or `sid` parameters. [F-DISC, F-LOGOUT, F-APP] | **No.** It came in 2025.10.0. [A-SRC `views/provider.py`, A-RN-2025.10] |
| RP-Initiated Logout 1.0 | **Yes.** `end_session_endpoint` = `/connect/endsession`. [D-DISC] | **Yes.** `end_session_endpoint` = `/protocol/openid-connect/logout`. Supports `id_token_hint`, `post_logout_redirect_uri`, and `client_id`. [K-DISC, K-ADMIN] | **Yes.** `end_session_endpoint` = `/oauth2/logout`. Supports `client_id`, `id_token_hint`, `post_logout_redirect_uri`, and `state`. The redirect URI must be in the authorized redirect URLs. [F-DISC, F-LOGOUT] | **Partial.** `end_session_endpoint` = `/application/o/<slug>/end-session/`. It ignores `id_token_hint`, `post_logout_redirect_uri`, and `state`. The user does not return to the RP. Full support came in 2026.5.0. [A-SRC `views/end_session.py`, A-RN-2026.5] |
| `sid` claim in the ID token | **Yes**, when a session exists. [D-TOK] | **Yes**, by default. Live test: `sid` in the ID, access, and refresh tokens. [K-UP25] | **Yes**, for interactive grants. It is the SSO session ID. The access token `sid` is the refresh token ID, a different value. [F-TOKENS] | **Yes**, when a session exists. The value is the SHA-256 hex of the session key. [A-SRC `id_token.py` lines 27-29, 128-129] |
| RFC 9207 `iss` in the authorization response | **Yes.** `authorization_response_iss_parameter_supported: true`. Default on. [D-DISC, D-OPT `EmitIssuerIdentificationResponseParameter`] | **Yes.** `authorization_response_iss_parameter_supported: true`. [K-DISC, K-WK] | **Yes**, since 1.69.0. `authorization_response_iss_parameter_supported: true`. The value is the tenant issuer. [F-DISC, F-ISS, F-RN] | **No.** The response has only `code` and `state`. Not added up to 2026.8.4. [A-SRC `views/authorize.py` lines 547-591] |
| PAR (RFC 9126) | **Yes.** `pushed_authorization_request_endpoint` = `/connect/par`, `require_pushed_authorization_requests: false`. [D-DISC, D-OPT] | **Yes.** `pushed_authorization_request_endpoint` = `/protocol/openid-connect/ext/par/request`, `require_pushed_authorization_requests: false`. Default feature. [K-DISC, K-WK, K-PROF] | **No.** No discovery key. `POST /oauth2/par` gave 404. [F-DISC, F-OAUTH] | **No.** No PAR endpoint. `request_parameter_supported: false`. Not added up to 2026.8.4. [A-SRC `views/authorize.py` lines 155-158, `urls.py`] |
| DPoP (RFC 9449) | **Yes.** `dpop_signing_alg_values_supported` lists RS, PS, and ES algorithms. Off for `interactive.confidential`. Other demo clients (for example `interactive.confidential.jwt.dpop`) set `RequireDPoP = true`. [D-DISC, D-CFG] | **No, by default.** Preview feature. It needs `--features=dpop`. No `dpop_signing_alg_values_supported` in the live discovery. [K-DISC, K-ADMIN, K-PROF] | **Enterprise plan only**, since 1.63.0. `dpop_signing_alg_values_supported` is present in discovery. [F-DISC, F-DPOP] | **No.** It came in 2026.8.0. [A-RN-2026.8] |
| Refresh token rotation | **Off for our client.** `interactive.confidential` sets `RefreshTokenUsage = TokenUsage.ReUse` and sliding expiration. `ReUse` is also the product default. [D-CFG, D-CLIENT] | **New token on each refresh, but the old token stays valid.** Realm defaults `revokeRefreshToken: false`, `refreshTokenMaxReuse: 0`. Live test: reuse of the old token gave 200. [K-ADMIN, K-TM] | **Off by default.** `refreshTokenUsagePolicy` defaults to `Reusable`. `OneTimeUse` rotates. Reuse detection (`onOneTimeTokenReuse`) is off by default and revokes only the reused token. [F-TENANT, F-APP] | **Yes**, always. The old refresh token is set to revoked. Reuse gives `invalid_grant` and a `SUSPICIOUS_REQUEST` event. It does not revoke the token family. [A-SRC `views/token.py` lines 305-313, 661-711] |
| `S256` PKCE | **Yes, required.** `code_challenge_methods_supported: ["plain","S256"]`. `RequirePkce` defaults to true. `AllowPlainTextPkce` defaults to false. [D-DISC, D-CLIENT] | **Yes.** `code_challenge_methods_supported: ["plain","S256"]`. Required when the client attribute `pkce.code.challenge.method` is set. [K-DISC, K-ADMIN, K-ATTR] | **Yes.** `code_challenge_methods_supported: ["S256"]`. Required only when `proofKeyForCodeExchangePolicy` is `Required` (default `NotRequired`). [F-DISC, F-AUTHZ, F-APP] | **Yes, optional.** `code_challenge_methods_supported: ["plain","S256"]`. No setting enforces it. A missing method defaults to `plain`. [A-SRC `views/authorize.py`, `views/token.py` lines 217-237, `views/provider.py`] |

## What our seed config enables

The seed files are in `FullstackTemplate.AppHost/`. The BFF sets `UsePkce = true` and requests `offline_access` for all providers (`FullstackTemplate.Bff/AuthExtensions.cs`). Duende BFF maps a back-channel logout endpoint, but no provider is configured to call it.

### Duende Demo (client `interactive.confidential`)

- The demo owns the client config. We cannot change it. [D-CFG]
- No `BackChannelLogoutUri` and no `FrontChannelLogoutUri`. The demo is on the public internet, so it cannot call a BFF on `localhost` in any case.
- `RedirectUris` and `PostLogoutRedirectUris` are `https://notused`. The demo replaces the redirect URI validator with `DemoRedirectValidator`, which accepts every URI. [demo `src/DemoRedirectValidator.cs`]
- `RefreshTokenUsage = ReUse`. No rotation.
- PKCE is required (product default).
- `Auth:RevokeRefreshTokenOnLogout` is `true`.

### Keycloak (`keycloak/realm-export.json`, client `aspire-app`)

- `pkce.code.challenge.method: S256`. PKCE is required with `S256`.
- `backchannel.logout.session.required: true`, but no `backchannel.logout.url`. Back-channel logout is not active.
- No `frontchannel.logout.url`.
- `post.logout.redirect.uris: "+"`. The post-logout redirect URIs are the same as the redirect URIs.
- `require.pushed.authorization.requests: false`. PAR is optional.
- The realm does not set `revokeRefreshToken`. The default is `false`, so reuse detection is off.
- `directAccessGrantsEnabled: true`. The password grant is on. OAuth 2.1 removes this grant.
- `Auth:RevokeRefreshTokenOnLogout` is `true`.

### FusionAuth (`fusionauth/kickstart.json`)

- `proofKeyForCodeExchangePolicy: Required`. PKCE is required.
- `requireClientAuthentication: false` and `clientAuthenticationPolicy: NotRequiredWhenUsingPKCE`. The token endpoint accepts a code exchange without the client secret.
- `logoutBehavior: RedirectOnly` and `logoutURL` = the frontend URL. FusionAuth does not call the logout URLs of other applications. No back-channel logout (not supported).
- `refreshTokenUsagePolicy` is not set. The default `Reusable` applies, so there is no rotation.
- The kickstart does not set the tenant issuer.
- `Auth:RevokeRefreshTokenOnLogout` is `false` and `Auth:UseAudienceAsScope` is `false`. The comments in `AuthProviders.cs` are still correct for 1.69.4: there is no RFC 7009 endpoint, and the default `unknownScopePolicy` is `Reject`. Custom scopes need the paid Essentials plan. [F-APP, https://fusionauth.io/docs/get-started/core-concepts/plans-features]
- The authorized redirect URLs include `/signout-callback-oidc` only for ports 7130 (http and https). They do not include `${APP_FRONTEND_URL}/signout-callback-oidc`. This was not tested.

### authentik (`authentik/default.yaml`)

- `client_type: confidential`. The client secret is checked on the code and refresh grants.
- PKCE is optional. The BFF sends `S256` because `UsePkce = true`.
- `offline_access` scope mapping and `refresh_token_validity: days=30`. Refresh tokens rotate on each refresh.
- `invalidation_flow: default-provider-invalidation-flow`. This flow has no user logout stage. The end-session endpoint does not end the authentik session, and the user stays on an authentik page. [https://github.com/goauthentik/authentik/blob/version/2024.12.5/blueprints/default/flow-default-provider-invalidation.yaml, https://github.com/goauthentik/authentik/blob/version/2024.12.5/authentik/flows/stage.py lines 283-308]
- `sub_mode: user_email`. The `sub` claim is the email.
- `Auth:RevokeRefreshTokenOnLogout` is `true`. The revoke endpoint exists, so this works.

## Effect on the map

- Back-Channel Logout as a receiver can be tested end to end with Keycloak only. The Duende Demo supports it but cannot reach a local BFF. FusionAuth and authentik 2024.12 do not support it.
- RFC 7009 revocation on logout must stay optional for each provider (FusionAuth has none). The BFF can decide from the presence of `revocation_endpoint` in discovery, in place of a config flag.
- RFC 9207 `iss` validation must accept a missing `iss` when discovery does not set `authorization_response_iss_parameter_supported` (authentik). RFC 9207 section 2.4 allows this.
- RP-Initiated Logout against authentik 2024.12 does not return the user to the app. An upgrade to authentik 2026.5 or later fixes this and adds Back-Channel and Front-Channel Logout.
- PAR (phase 2) works with the Duende Demo and Keycloak only. DPoP (phase 2) works with the Duende Demo by default, with Keycloak only behind a preview flag, and with FusionAuth only on a paid plan.
- No provider in our config enforces refresh token reuse detection that revokes the token family. The BFF must not depend on rotation.

## Open questions

- Upgrade authentik from 2024.12 to 2026.5 or later? That version adds RP-Initiated Logout with `post_logout_redirect_uri`, Back-Channel Logout (Preview since 2025.8), and Front-Channel Logout.
- Pin FusionAuth to a version in place of `latest`? The RFC 9207 support arrived in 1.69.0, so behavior changes with each release.
- Register a back-channel logout URL in the Keycloak seed, so that e2e tests can prove Back-Channel Logout? Keycloak runs in a container, so the URL must be reachable from the container network.
- Does the FusionAuth logout redirect fail in an isolated run, because `${APP_FRONTEND_URL}/signout-callback-oidc` is not an authorized redirect URL? Not tested.
- Turn off `directAccessGrantsEnabled` in the Keycloak seed for OAuth 2.1?
