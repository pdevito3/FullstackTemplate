# The implicit behavior of Duende BFF v4

Research for issue #7 of the map #6 "Migrate the BFF off Duende BFF".

## Question

Which behaviors does the current BFF get from `Duende.BFF` 4.0.1 and `Duende.BFF.Yarp` 4.0.1 by default, with no code of ours? The home-rolled BFF must keep each one, or the spec must drop it on purpose.

## Answer in short

`AddBff()` and `UseBff()` do much more than our code shows. With our configuration, Duende BFF:

1. Adds a startup filter that puts `UseForwardedHeaders`, an early OIDC callback handler, and the management endpoints into the pipeline. Our `Program.cs` does not show these.
2. Registers two authentication schemes on the fly (`duende-bff-cookie` and `duende-bff-oidc`), and makes them the defaults.
3. Maps six endpoints under `/bff`: `login`, `logout`, `user`, `silent-login`, `silent-login-callback`, `backchannel`, and `diagnostics` (seven paths in total).
4. Requires `X-CSRF: 1` on each endpoint that has BFF API metadata (`/bff/user`, `/local-api/hello`, and `/api/*`). It answers 401 with an empty body if the header is missing.
5. Changes a 302 challenge into a 401, and a 302 forbid into a 403, on BFF API endpoints. It removes `Location` and all `Set-Cookie` headers from that response.
6. Requires `sid` on `/bff/logout` for a user whose session has a `sid` claim. It accepts only a local `returnUrl` on login and logout.
7. Revokes the refresh token when the cookie signs out.
8. Proxies `/api/*` with YARP. It removes the `Cookie` header, strips the `/api` prefix, adds `X-Forwarded-*`, and attaches the user access token. With `UserOrNone`, a failed token refresh sends the request with no token, and the session stays.
9. Calls `AddOpenIdConnectAccessTokenManagement()` a second time (we call it too). That wires token storage in the cookie, refresh before expiry, and in-process de-duplication of concurrent refreshes.

The sliding session, the 14-day lifetime, and the `HttpOnly` cookie come from the Microsoft cookie handler, not from Duende. Duende only reports the expiry as `bff:session_expires_in`.

Two behaviors do not work today. Back-channel logout returns 200 but does nothing, because we have no server-side sessions. The diagnostics endpoint is on in Development and shows the raw access token. See [Gaps and open questions](#gaps-and-open-questions).

## Sources and versions

| Package | Version | Source |
|---|---|---|
| `Duende.BFF`, `Duende.BFF.Yarp` | 4.0.1 | [DuendeSoftware/products, tag `bff-4.0.1`](https://github.com/DuendeSoftware/products/tree/bff-4.0.1/bff/src) (commit `fcf7456`) |
| `Duende.AccessTokenManagement.OpenIdConnect` | 4.1.0 (transitive, from the `Duende.BFF` nuspec) | [DuendeSoftware/foss, tag `atm-4.1.0`](https://github.com/DuendeSoftware/foss/tree/atm-4.1.0/access-token-management/src) (commit `bfe1851`) |
| `Yarp.ReverseProxy` | 2.1.0 (transitive, from the `Duende.BFF.Yarp` nuspec) | [dotnet/yarp, tag `v2.1.0`](https://github.com/dotnet/yarp/tree/v2.1.0/src/ReverseProxy) |
| ASP.NET Core cookie and OIDC handlers | .NET 10 | [dotnet/aspnetcore, `release/10.0`](https://github.com/dotnet/aspnetcore/tree/release/10.0/src/Security/Authentication) |

Docs: [BFF session management](https://docs.duendesoftware.com/bff/fundamentals/session/management/), [remote APIs](https://docs.duendesoftware.com/bff/fundamentals/apis/remote/), [local APIs](https://docs.duendesoftware.com/bff/fundamentals/apis/local/), [back-channel logout](https://docs.duendesoftware.com/bff/fundamentals/session/management/back-channel-logout/), [tokens](https://docs.duendesoftware.com/bff/fundamentals/tokens/).

Link prefixes in this file:

- `BFF/` is `https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/`
- `YARPBFF/` is `https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff.Yarp/`
- `ATM/` is `https://github.com/DuendeSoftware/foss/blob/atm-4.1.0/access-token-management/src/AccessTokenManagement.OpenIdConnect/`
- `YARP/` is `https://github.com/dotnet/yarp/blob/v2.1.0/src/ReverseProxy/`

## What our code calls

`FullstackTemplate.Bff/AuthExtensions.cs`:

- `services.AddOpenIdConnectAccessTokenManagement(o => o.RefreshBeforeExpiration = 120s)`
- `services.AddBff(o => o.RevokeRefreshTokenOnLogout = <config, default true>)`
- `.ConfigureOpenIdConnect(...)`: authority, client id and secret, `code` with PKCE, `response_mode=query`, `SaveTokens`, `GetClaimsFromUserInfoEndpoint`, `MapInboundClaims=false`, scopes `openid profile email offline_access` and an optional audience scope, name and role claim types, two trace events.
- `.ConfigureCookies(...)`: name `__Host-bff`, `SameSite=Lax`, `SecurePolicy=SameAsRequest`.
- `.AddRemoteApis()`

`FullstackTemplate.Bff/Program.cs`:

- `UseForwardedHeaders`, Serilog request logging, `UseExceptionHandler`, `UseCors`, `UseAuthentication`, `UseRouting`, `UseBff`, `UseAuthorization`.
- `MapGet("/local-api/hello").AsBffApiEndpoint()`
- `MapRemoteBffApiEndpoint("/api", <server>/api).WithAccessToken(RequiredTokenType.UserOrNone)`

We do not call `AddServerSideSessions`, `AddFrontends`, `MapBffManagementEndpoints`, or set a license key.

## 1. Pipeline wiring that `AddBff` adds

`AddBff` registers `ConfigureBffStartupFilter` ([BFF/BffBuilderExtensions.cs#L145](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/BffBuilderExtensions.cs#L145)). `AutomaticallyRegisterBffMiddleware` is `true` by default ([BFF/Configuration/BffOptions.cs#L168](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Configuration/BffOptions.cs#L168)). The filter wraps our whole pipeline ([BFF/DynamicFrontends/Internal/ConfigureBffStartupFilter.cs#L14-L33](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/DynamicFrontends/Internal/ConfigureBffStartupFilter.cs#L14-L33)):

Before our middleware:

1. `app.UseForwardedHeaders()`. It uses our `ForwardedHeadersOptions`, so it trusts all proxies. Our own `UseForwardedHeaders` then runs a second time and finds nothing to do.
2. `UseBffFrontendSelection`, `UseBffPathMapping`: no-ops, because we have no frontends ([BFF/DynamicFrontends/Internal/FrontendSelector.cs#L36-L47](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/DynamicFrontends/Internal/FrontendSelector.cs#L36-L47)).
3. `UseBffOpenIdCallbacks`: on `CallbackPath` (`/signin-oidc`) and `SignedOutCallbackPath` (`/signout-callback-oidc`), it calls the OIDC handler directly and ends the request ([BFF/DynamicFrontends/Internal/OpenIdConnectCallbackMiddleware.cs#L61-L70](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/DynamicFrontends/Internal/OpenIdConnectCallbackMiddleware.cs#L61-L70), [#L90-L95](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/DynamicFrontends/Internal/OpenIdConnectCallbackMiddleware.cs#L90-L95)). Thus the login callback runs before our Serilog request logging, CORS, and exception handler. `/signout-oidc` (front-channel logout) is not in this list, so `UseAuthentication` handles it later.

After our middleware:

4. `UseBffRemoteRoutes` (added by `AddRemoteApis`, [YARPBFF/BffBuilderExtensions.cs#L45-L51](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff.Yarp/BffBuilderExtensions.cs#L45-L51)). It only acts for frontends, so it is a no-op for us ([YARPBFF/Internal/RemoteRouteHandler.cs#L71-L76](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff.Yarp/Internal/RemoteRouteHandler.cs#L71-L76)).
5. `UseEndpoints` with the seven management endpoints ([BFF/BffApplicationBuilderExtensions.cs#L65-L90](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/BffApplicationBuilderExtensions.cs#L65-L90)).
6. `UseBffStaticFileProxying`: no-op without frontends.

`UseBff()` itself only adds `BffAntiForgeryMiddleware` ([BFF/BffApplicationBuilderExtensions.cs#L22](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/BffApplicationBuilderExtensions.cs#L22)). It must run after `UseRouting` and before `UseAuthorization`, because it reads the endpoint metadata. Each management endpoint throws `InvalidOperationException` if this middleware did not run (`EnforceBffMiddleware`, [BFF/HttpContextExtensions.cs#L31-L42](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/HttpContextExtensions.cs#L31-L42)).

## 2. Authentication schemes and the cookie

- Scheme names: `duende-bff-cookie` and `duende-bff-oidc` ([BFF/DynamicFrontends/BffAuthenticationSchemes.cs#L11-L12](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/DynamicFrontends/BffAuthenticationSchemes.cs#L11-L12)). A custom `IAuthenticationSchemeProvider` creates them on demand. We never call `AddCookie` or `AddOpenIdConnect` ([BFF/DynamicFrontends/Internal/BffAuthenticationSchemeProvider.cs#L20-L64](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/DynamicFrontends/Internal/BffAuthenticationSchemeProvider.cs#L20-L64)).
- Defaults, if we set none: `DefaultScheme` and `DefaultForbidScheme` are the cookie. `DefaultChallengeScheme` and `DefaultSignOutScheme` are OIDC ([BFF/DynamicFrontends/BffConfigureAuthenticationOptions.cs#L11-L29](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/DynamicFrontends/BffConfigureAuthenticationOptions.cs#L11-L29)).
- BFF registers the `OpenIdConnectPostConfigureOptions` and `PostConfigureCookieAuthenticationOptions` that `AddOpenIdConnect` and `AddCookie` normally add ([BFF/BffBuilderExtensions.cs#L167-L172](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/BffBuilderExtensions.cs#L167-L172)).
- Cookie defaults from BFF: `HttpOnly=true`, `SecurePolicy=SameAsRequest`, `SameSite=Strict`, name `__Host-bff-auth`. Then BFF applies our `ConfigureCookies` delegate, which changes the name to `__Host-bff` and `SameSite` to `Lax` ([BFF/DynamicFrontends/BffConfigureCookieOptions.cs#L43-L60](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/DynamicFrontends/BffConfigureCookieOptions.cs#L43-L60), [BFF/Constants.cs#L36-L39](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Constants.cs#L36-L39)).
- From the Microsoft cookie handler, not from Duende: `ExpireTimeSpan=14 days`, `SlidingExpiration=true` ([aspnetcore CookieAuthenticationOptions.cs#L30-L32](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Security/Authentication/Cookies/src/CookieAuthenticationOptions.cs#L30-L32)). The cookie handler also splits a large cookie into chunks (`__Host-bffC1`, `__Host-bffC2`, ...). The cookie holds the access, refresh, and id tokens, because we set `SaveTokens=true`.
- From the Microsoft OIDC handler, not from Duende: `/signin-oidc`, `/signout-callback-oidc`, `/signout-oidc`, and `PushedAuthorizationBehavior=UseIfAvailable` ([aspnetcore OpenIdConnectOptions.cs#L44-L46](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectOptions.cs#L44-L46), [#L419](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectOptions.cs#L419)).

## 3. The management endpoints

All paths are `ManagementBasePath` (`/bff`) plus a suffix ([BFF/Constants.cs#L65-L102](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Constants.cs#L65-L102)). The mapping is in [BFF/BffEndpointRouteBuilderExtensions.cs#L62-L189](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/BffEndpointRouteBuilderExtensions.cs#L62-L189). Each is `AllowAnonymous`. A path that the app already maps is skipped ([#L191-L197](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/BffEndpointRouteBuilderExtensions.cs#L191-L197)).

| Path | Verb | Metadata | Used by our frontend |
|---|---|---|---|
| `/bff/login` | any (`Map`) | UI endpoint | yes |
| `/bff/silent-login` | GET | UI endpoint, obsolete | no |
| `/bff/silent-login-callback` | GET | UI endpoint | no |
| `/bff/logout` | GET | UI endpoint | yes |
| `/bff/user` | GET | BFF API (`X-CSRF` required) | yes |
| `/bff/backchannel` | POST | none | no (not registered at any IdP) |
| `/bff/diagnostics` | GET | none | no |

### 3.1 `/bff/login`

Source: [BFF/Endpoints/Internal/DefaultLoginEndpoint.cs#L30-L127](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Endpoints/Internal/DefaultLoginEndpoint.cs#L30-L127).

1. Reads `returnUrl` and `prompt` from the query.
2. Loads the discovery document. If the options have no configuration, it returns 404. If `prompt` is set and is not in `prompt_values_supported`, it returns 400 `application/problem+json` with the title "Invalid prompt value".
3. If `returnUrl` is set and is not local, it returns 400 problem "Invalid return url".
4. If `returnUrl` is empty, it uses `PathBase` or `/`.
5. Calls `ChallengeAsync` on the default (OIDC) scheme with `RedirectUri=returnUrl`. It stores `prompt` in `AuthenticationProperties.Items["bff-prompt"]`.

The local URL rule ([BFF/Internal/LocalUrlReturnUrlValidator.cs#L15-L62](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Internal/LocalUrlReturnUrlValidator.cs#L15-L62)): the value must be `/`, or start with `/` and a second character that is not `/` or `\`. No ASCII control character is allowed. Absolute URLs and `//host` are rejected.

The OIDC events that BFF adds ([BFF/Endpoints/Internal/PostConfigureOidcOptionsForSilentLogin.cs#L24-L44](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Endpoints/Internal/PostConfigureOidcOptionsForSilentLogin.cs#L24-L44), [BFF/Endpoints/Internal/BffOpenIdConnectEvents.cs#L31-L94](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Endpoints/Internal/BffOpenIdConnectEvents.cs#L31-L94)):

- `OnRedirectToIdentityProvider`: copies `bff-prompt` to the `prompt` parameter. For `prompt=none`, it also changes the redirect to `/bff/silent-login-callback`.
- `OnMessageReceived`: if a login with a prompt gets an `error` from the IdP (for example `login_required`), it redirects to the `RedirectUri` and does not fail the request.
- If `OpenIdConnectOptions.EventsType` is set, it must derive from `BffOpenIdConnectEvents`, or startup throws.

### 3.2 `/bff/logout`

Source: [BFF/Endpoints/Internal/DefaultLogoutEndpoint.cs#L25-L87](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Endpoints/Internal/DefaultLogoutEndpoint.cs#L25-L87).

1. Authenticates the cookie. If the user is signed in and has a `sid` claim, the query must contain the same `sid`. If not, it returns 400 problem "Invalid Session ID" (`RequireLogoutSessionId=true`, [BFF/Configuration/BffOptions.cs#L29](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Configuration/BffOptions.cs#L29)). A user with no `sid` claim, or an anonymous user, needs no `sid`.
2. Validates `returnUrl` with the local URL rule. It returns 400 problem if the value is not local.
3. Signs out the cookie scheme. This fires `OnSigningOut`, which revokes the refresh token (section 6.4).
4. Calls `SignOutAsync` on the default sign-out scheme (OIDC) with `RedirectUri=returnUrl` or `/`. The Microsoft handler then sends the browser to `end_session_endpoint` with `id_token_hint` and `post_logout_redirect_uri=/signout-callback-oidc`.

The `sid` check is the CSRF defense of a GET logout. The frontend gets the URL with `sid` from the `bff:logout_url` claim.

### 3.3 `/bff/user`

Source: [BFF/Endpoints/Internal/DefaultUserEndpoint.cs#L29-L112](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Endpoints/Internal/DefaultUserEndpoint.cs#L29-L112).

- It is a BFF API endpoint ([BFF/BffEndpointRouteBuilderExtensions.cs#L146-L148](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/BffEndpointRouteBuilderExtensions.cs#L146-L148)). Without `X-CSRF: 1`, the anti-forgery middleware returns 401 before the endpoint runs.
- Anonymous: 401 with an empty body (`AnonymousSessionResponse.Response401`, [BFF/Configuration/BffOptions.cs#L119](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Configuration/BffOptions.cs#L119)). The other option is 200 with the body `null`.
- Signed in: 200 `application/json`, an array of `{ "type", "value", "valueType" }` ([BFF/Internal/ClaimRecord.cs#L27-L40](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Internal/ClaimRecord.cs#L27-L40)). `valueType` is always `null`. The array contains:
  - every claim of the cookie principal, in order.
  - `bff:logout_url`, if the principal does not have it: `PathBase + /bff/logout`, plus `?sid=<url-encoded sid>` if a `sid` claim exists ([BFF/Internal/LogoutUrlBuilder.cs#L24-L33](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Internal/LogoutUrlBuilder.cs#L24-L33)).
  - `bff:session_expires_in`: `ExpiresUtc - now`, rounded to whole seconds, as a JSON number (not a string).
  - `bff:session_state`, if the authorize response had `session_state`.
- `?slide=false` stops the cookie from sliding on this one request ([BFF/SessionManagement/Configuration/PostConfigureSlidingExpirationCheck.cs#L36-L55](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/SessionManagement/Configuration/PostConfigureSlidingExpirationCheck.cs#L36-L55)). Our frontend does not use it.

### 3.4 `/bff/silent-login` and `/bff/silent-login-callback`

- `silent-login` is obsolete. It calls `ChallengeAsync` with `prompt=none` ([BFF/Endpoints/Internal/DefaultSilentLoginEndpoint.cs#L26-L43](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Endpoints/Internal/DefaultSilentLoginEndpoint.cs#L26-L43)). The replacement is `/bff/login?prompt=none`.
- `silent-login-callback` returns an HTML page with a nonce CSP and `no-store`. The page posts `{source:'bff-silent-login', isLoggedIn:true|false}` to `window.parent`, with the BFF origin as the target ([BFF/Endpoints/Internal/DefaultSilentLoginCallbackEndpoint.cs#L24-L59](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Endpoints/Internal/DefaultSilentLoginCallbackEndpoint.cs#L24-L59)).

Our frontend uses neither. Third-party cookie rules in browsers block the iframe flow for most IdPs.

### 3.5 `/bff/backchannel`

Source: [BFF/Endpoints/Internal/DefaultBackchannelLogoutEndpoint.cs#L30-L189](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Endpoints/Internal/DefaultBackchannelLogoutEndpoint.cs#L30-L189).

1. Sets `Cache-Control: no-cache, no-store` and `Pragma: no-cache`.
2. Reads the form field `logout_token`. If it is missing or invalid, it returns 400.
3. Validates the JWT: issuer from discovery, audience equal to `ClientId`, signing keys from discovery, and the default lifetime check.
4. Requires `sub` or `sid`, no `nonce`, and an `events` claim with the member `http://schemas.openid.net/event/backchannel-logout`.
5. Calls `ISessionRevocationService.RevokeSessionsAsync(sub, sid)` and returns 200.

With no `AddServerSideSessions`, the service is `NopSessionRevocationService`. It only logs ([BFF/BffBuilderExtensions.cs#L86](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/BffBuilderExtensions.cs#L86), [BFF/SessionManagement/Revocation/NopSessionRevocationService.cs#L16-L20](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/SessionManagement/Revocation/NopSessionRevocationService.cs#L16-L20)). The Duende docs say: "The back-channel logout endpoint depends on server-side sessions in the BFF, which must be enabled to use this endpoint." ([docs](https://docs.duendesoftware.com/bff/fundamentals/session/management/back-channel-logout/)). Thus today the endpoint answers 200 and ends no session.

The validation also does not check the `typ` header (`logout+jwt`), `iat`, or `jti` replay.

### 3.6 `/bff/diagnostics`

Source: [BFF/Endpoints/Internal/DefaultDiagnosticsEndpoint.cs#L27-L61](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Endpoints/Internal/DefaultDiagnosticsEndpoint.cs#L27-L61).

- Outside the `Development` environment, it returns 404 ([BFF/Configuration/BffOptions.cs#L125-L128](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Configuration/BffOptions.cs#L125-L128)).
- In Development, it gets a user token and a client credentials token. If both work, it returns `{ "UserAccessToken", "ClientAccessToken" }` as JSON. If one fails, it returns 200 with `{ "Error", "ErrorDescription" }`.
- It has no `X-CSRF` check and no authorization.

## 4. CSRF and the status code rules

### 4.1 The `X-CSRF` check

Source: [BFF/Endpoints/Internal/BffAntiForgeryMiddleware.cs#L24-L62](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Endpoints/Internal/BffAntiForgeryMiddleware.cs#L24-L62), [BFF/HttpContextExtensions.cs#L44-L48](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/HttpContextExtensions.cs#L44-L48).

- The check applies to each endpoint with `IBffApiMetadata`: `AsBffApiEndpoint()` ([BFF/EndpointConventionBuilderExtensions.cs#L20](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/EndpointConventionBuilderExtensions.cs#L20)), `/bff/user`, and each `MapRemoteBffApiEndpoint` ([BFF/Configuration/BffRemoteApiEndpointMetadata.cs#L12](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Configuration/BffRemoteApiEndpointMetadata.cs#L12)).
- The header name is `X-CSRF` and the value must equal `1` exactly. Only the first value counts.
- It applies to every HTTP method, GET included.
- On failure: status 401, empty body, no `WWW-Authenticate`. The request ends there.
- `SkipAntiforgery()` and `BffOptions.DisableAntiForgeryCheck` turn it off. We use neither.
- The check runs whether the user is signed in or not. Thus `/local-api/hello` and anonymous `/api/*` calls also need the header.

### 4.2 302 to 401 and 403

Source: [BFF/Endpoints/Internal/BffAuthenticationService.cs#L59-L111](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Endpoints/Internal/BffAuthenticationService.cs#L59-L111). BFF decorates `IAuthenticationService` ([BFF/BffBuilderExtensions.cs#L108-L109](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/BffBuilderExtensions.cs#L108-L109)).

- After a challenge: if the status is 302 and the endpoint has `IBffApiMetadata`, the status becomes 401.
- After a forbid: if the status is 302 and the endpoint has `IBffApiMetadata`, the status becomes 403.
- In both cases it removes `Location` and every `Set-Cookie` header. This also drops the OIDC nonce and correlation cookies.
- `SkipResponseHandling()` turns it off.
- No endpoint of ours has an authorization policy today. Thus the rule fires only if a future `RequireAuthorization()` endpoint gets `AsBffApiEndpoint()`. The server API returns its own 401 and 403, and the proxy passes them through.

The login, logout, and silent login endpoints have `BffUiEndpointAttribute`, not `IBffApiMetadata`. Their 302 stays a 302.

## 5. The session

- The session is the cookie only. There is no server-side store, because we do not call `AddServerSideSessions` ([BFF/BffBuilderExtensions.cs#L206-L229](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/BffBuilderExtensions.cs#L206-L229)).
- Lifetime: 14 days from the Microsoft cookie handler. `SlidingExpiration=true` renews the cookie on a request after half of the lifetime is gone. The OIDC handler has `UseTokenLifetime=false`, so the IdP token lifetime does not set the cookie lifetime.
- Each token refresh also re-issues the cookie with a new `IssuedUtc` and `ExpiresUtc` (when sliding is on). Thus a refresh restarts the 14 days ([ATM/Internal/StoreTokensInAuthenticationProperties.cs#L131-L138](https://github.com/DuendeSoftware/foss/blob/atm-4.1.0/access-token-management/src/AccessTokenManagement.OpenIdConnect/Internal/StoreTokensInAuthenticationProperties.cs#L131-L138), [ATM/Internal/AuthenticationSessionUserAccessTokenStore.cs#L99-L108](https://github.com/DuendeSoftware/foss/blob/atm-4.1.0/access-token-management/src/AccessTokenManagement.OpenIdConnect/Internal/AuthenticationSessionUserAccessTokenStore.cs#L99-L108)).
- `bff:session_expires_in` is computed on each `/bff/user` call (section 3.3).
- BFF adds `OnSigningIn` for the trial license check (section 8) and `OnSigningOut` for refresh token revocation (section 6.4) to the cookie events.

## 6. Access token management

### 6.1 What `AddBff` wires

[BFF/BffBuilderExtensions.cs#L61-L72](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/BffBuilderExtensions.cs#L61-L72):

- `IConfigureOptions<OpenIdConnectOptions>` = `BffConfigureOpenIdConnectOptions` (applies our `ConfigureOpenIdConnect` delegate).
- `services.AddOpenIdConnectAccessTokenManagement()`.
- `ConfigureUserTokenManagementOptions`: copies `BffOptions.DPoPJsonWebKey` into the ATM options ([BFF/Configuration/ConfigureUserTokenManagementOptions.cs#L16-L22](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Configuration/ConfigureUserTokenManagementOptions.cs#L16-L22)). It is `null` for us.
- `IAccessTokenRetriever` = `DefaultAccessTokenRetriever`, which calls `GetManagedAccessToken` ([BFF/Internal/DefaultAccessTokenRetriever.cs#L15-L27](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Internal/DefaultAccessTokenRetriever.cs#L15-L27)).
- `AddDistributedMemoryCache()` and `AddHybridCache()` ([BFF/BffBuilderExtensions.cs#L59](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/BffBuilderExtensions.cs#L59), [#L141](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/BffBuilderExtensions.cs#L141)). ATM client credentials tokens use the cache. User tokens do not, because they live in the cookie.

### 6.2 What `AddOpenIdConnectAccessTokenManagement` wires

[ATM/ServiceCollectionExtensions.cs#L22-L51](https://github.com/DuendeSoftware/foss/blob/atm-4.1.0/access-token-management/src/AccessTokenManagement.OpenIdConnect/ServiceCollectionExtensions.cs#L22-L51):

- Client credentials token management, configured from the OIDC options of the default challenge scheme.
- `IUserTokenManager`, `IUserTokenStore` = `AuthenticationSessionUserAccessTokenStore` (tokens in the cookie `AuthenticationProperties`), `IUserTokenRequestConcurrencyControl`, `IOpenIdConnectUserTokenEndpoint`, `IOpenIdConnectConfigurationService`, a per-request `AuthenticateResultCache`.
- `ConfigureOpenIdConnectOptions` for the default challenge scheme. It wraps `OnRedirectToIdentityProvider`, `OnAuthorizationCodeReceived`, and `OnTokenValidated` for DPoP. It also wraps `BackchannelHttpHandler` in `AuthorizationServerDPoPHandler` ([ATM/Internal/ConfigureOpenIdConnectOptions.cs#L53-L67](https://github.com/DuendeSoftware/foss/blob/atm-4.1.0/access-token-management/src/AccessTokenManagement.OpenIdConnect/Internal/ConfigureOpenIdConnectOptions.cs#L53-L67)). Without a DPoP key, these wrappers do nothing.

We call `AddOpenIdConnectAccessTokenManagement` in `AuthExtensions.cs`, and `AddBff` calls it again. `ConfigureOptions<T>` uses `AddTransient` with no de-duplication ([dotnet/runtime OptionsServiceCollectionExtensions.cs#L212-L222](https://github.com/dotnet/runtime/blob/release/10.0/src/libraries/Microsoft.Extensions.Options/src/OptionsServiceCollectionExtensions.cs#L212-L222)). Thus `ConfigureOpenIdConnectOptions` runs two times: the events are wrapped two times, and there are two DPoP handlers in the backchannel. This is harmless without DPoP. The new BFF must call it one time.

The BFF source says that `BffConfigureOpenIdConnectOptions` must be registered before `AddOpenIdConnectAccessTokenManagement` ([BFF/BffBuilderExtensions.cs#L61-L64](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/BffBuilderExtensions.cs#L61-L64)). Our first call breaks that order. It is harmless today, because our delegate does not set `BackchannelHttpHandler`.

### 6.3 Refresh behavior

[ATM/Internal/UserAccessTokenManagementService.cs#L28-L112](https://github.com/DuendeSoftware/foss/blob/atm-4.1.0/access-token-management/src/AccessTokenManagement.OpenIdConnect/Internal/UserAccessTokenManagementService.cs#L28-L112):

- Anonymous user: failure "No active user".
- No refresh token in the cookie: returns the stored access token, even if it is expired.
- Refresh when the token expires within `RefreshBeforeExpiration`. ATM default is 1 minute. We set 120 seconds.
- Concurrent refreshes with the same refresh token share one request. This works in one process only. A `ConcurrentDictionary` keyed by the refresh token does it ([ATM/Internal/UserTokenRequestConcurrencyControl.cs#L14-L30](https://github.com/DuendeSoftware/foss/blob/atm-4.1.0/access-token-management/src/AccessTokenManagement.OpenIdConnect/Internal/UserTokenRequestConcurrencyControl.cs#L14-L30)).
- The refresh request uses `ClientCredentialStyle.AuthorizationHeader` (HTTP Basic) by default ([ATM/UserTokenManagementOptions.cs#L47](https://github.com/DuendeSoftware/foss/blob/atm-4.1.0/access-token-management/src/AccessTokenManagement.OpenIdConnect/UserTokenManagementOptions.cs#L47), [ATM/Internal/OpenIdConnectUserTokenEndpoint.cs#L50](https://github.com/DuendeSoftware/foss/blob/atm-4.1.0/access-token-management/src/AccessTokenManagement.OpenIdConnect/Internal/OpenIdConnectUserTokenEndpoint.cs#L50)). The Microsoft OIDC handler sends the secret in the form body for the code exchange. Thus the two token requests use different client authentication.
- If the response has no new refresh token, it keeps the old one ([ATM/Internal/OpenIdConnectUserTokenEndpoint.cs#L147](https://github.com/DuendeSoftware/foss/blob/atm-4.1.0/access-token-management/src/AccessTokenManagement.OpenIdConnect/Internal/OpenIdConnectUserTokenEndpoint.cs#L147)).
- On success, the store writes the new tokens into the properties and calls `SignInAsync`. The new cookie goes out in the same response as the proxied API response. YARP appends the API response headers to the headers that already exist, so the `Set-Cookie` stays ([YARP/Forwarder/HttpTransformer.cs#L250-L273](https://github.com/dotnet/yarp/blob/v2.1.0/src/ReverseProxy/Forwarder/HttpTransformer.cs#L250-L273)).
- The store sets `.Token.id_token` to the `id_token` of the refresh response, or to `null` if the response has none ([ATM/Internal/StoreTokensInAuthenticationProperties.cs#L113-L116](https://github.com/DuendeSoftware/foss/blob/atm-4.1.0/access-token-management/src/AccessTokenManagement.OpenIdConnect/Internal/StoreTokensInAuthenticationProperties.cs#L113-L116)). This is an open question (see below).

### 6.4 Refresh token revocation on logout

- `RevokeRefreshTokenOnLogout` defaults to `true` ([BFF/Configuration/BffOptions.cs#L35](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Configuration/BffOptions.cs#L35)). We read it from `Auth:RevokeRefreshTokenOnLogout`. FusionAuth sets it to `false` (`FullstackTemplate.AppHost/AuthProviders.cs`).
- The hook is the cookie `OnSigningOut` event ([BFF/SessionManagement/Configuration/PostConfigureApplicationCookieRevokeRefreshToken.cs#L28-L54](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/SessionManagement/Configuration/PostConfigureApplicationCookieRevokeRefreshToken.cs#L28-L54)). Thus it fires on each cookie sign-out: `/bff/logout`, and the sign-out after a failed refresh for `User` tokens.
- ATM sends an RFC 7009 request with `token_type_hint=refresh_token`. If discovery has no `revocation_endpoint`, it throws `InvalidOperationException` ([ATM/Internal/OpenIdConnectUserTokenEndpoint.cs#L172-L173](https://github.com/DuendeSoftware/foss/blob/atm-4.1.0/access-token-management/src/AccessTokenManagement.OpenIdConnect/Internal/OpenIdConnectUserTokenEndpoint.cs#L172-L173)), and the logout fails. That is why FusionAuth turns it off. If the IdP returns an error, ATM logs it and the logout continues ([#L208-L211](https://github.com/DuendeSoftware/foss/blob/atm-4.1.0/access-token-management/src/AccessTokenManagement.OpenIdConnect/Internal/OpenIdConnectUserTokenEndpoint.cs#L208-L211)).

## 7. The remote API proxy (`/api`)

### 7.1 Route and transforms

`MapRemoteBffApiEndpoint("/api", <server>/api)` ([YARPBFF/RouteBuilderExtensions.cs#L34-L75](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff.Yarp/RouteBuilderExtensions.cs#L34-L75)):

- Calls YARP `MapForwarder` with the pattern `/api/{**catch-all}`, so `/api` itself also matches. Route matching is case-insensitive.
- Uses `ForwarderRequestConfig.Empty`, unless DI has a `ForwarderRequestConfig`. Thus YARP defaults apply: 100-second activity timeout, and errors map to 502 or 504 ([YARP/Forwarder/HttpForwarder.cs#L593](https://github.com/dotnet/yarp/blob/v2.1.0/src/ReverseProxy/Forwarder/HttpForwarder.cs#L593), [#L655-L659](https://github.com/dotnet/yarp/blob/v2.1.0/src/ReverseProxy/Forwarder/HttpForwarder.cs#L655-L659)).
- Adds `BffRemoteApiEndpointMetadata`. That metadata has `IBffApiMetadata`, so section 4 applies. `WithAccessToken(UserOrNone)` sets its `TokenType` ([BFF/BffRemoteApiEndpointExtensions.cs#L32-L40](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/BffRemoteApiEndpointExtensions.cs#L32-L40)).
- Has no authorization policy. The server API decides 401 and 403.

The default BFF transforms ([YARPBFF/DefaultBffYarpTransformerBuilders.cs#L18-L24](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff.Yarp/DefaultBffYarpTransformerBuilders.cs#L18-L24)):

1. Remove the request header `Cookie`.
2. Remove the path prefix `/api`. We add `/api` back in the destination, so `/api/v1/x` goes to `<server>/api/v1/x`.
3. Add `AccessTokenRequestTransform`.

The YARP defaults that also apply ([YARP/Transforms/Builder/TransformBuilder.cs#L155-L178](https://github.com/dotnet/yarp/blob/v2.1.0/src/ReverseProxy/Transforms/Builder/TransformBuilder.cs#L155-L178), [YARP/Transforms/ForwardedTransformExtensions.cs#L116-L126](https://github.com/dotnet/yarp/blob/v2.1.0/src/ReverseProxy/Transforms/ForwardedTransformExtensions.cs#L116-L126)):

- Copy all other request headers. This includes `X-CSRF` and an `Authorization` header from the browser (see open questions).
- Do not keep the original `Host`. The destination host is used.
- Set `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`, and `X-Forwarded-Prefix` (action `Set`, so it replaces the incoming values). Remove `Forwarded`.
- Drop the hop-by-hop headers (`Connection`, `Transfer-Encoding`, `Keep-Alive`, `Upgrade`, `Proxy-*`, `TE`, and others, [YARP/Forwarder/RequestUtilities.cs#L71-L106](https://github.com/dotnet/yarp/blob/v2.1.0/src/ReverseProxy/Forwarder/RequestUtilities.cs#L71-L106)).
- Copy all response headers and trailers, `Set-Cookie` from the API included.

### 7.2 Token attach and failure

[YARPBFF/Internal/AccessTokenRequestTransform.cs#L29-L182](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff.Yarp/Internal/AccessTokenRequestTransform.cs#L29-L182) and [BFF/HttpContextExtensions.cs#L50-L171](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/HttpContextExtensions.cs#L50-L171):

| Case | Result with `UserOrNone` (ours) | Result with `User` |
|---|---|---|
| Anonymous | Forward with no token added | 401 from the BFF, request not forwarded |
| Signed in, token valid or refreshed | `Authorization: Bearer <token>` (replaces any incoming value) | same |
| Signed in, refresh fails | Forward with no token added. The session stays. | Sign out the cookie (and revoke), then 401 from the BFF |
| Token type `DPoP` | `Authorization: DPoP <token>` and a `DPoP` proof header | same |

- A 401 from the transform short-circuits YARP: YARP does not forward when a transform sets a status other than 200 ([YARP/Forwarder/HttpForwarder.cs#L150-L155](https://github.com/dotnet/yarp/blob/v2.1.0/src/ReverseProxy/Forwarder/HttpForwarder.cs#L150-L155), [YARP/Forwarder/RequestUtilities.cs#L419-L423](https://github.com/dotnet/yarp/blob/v2.1.0/src/ReverseProxy/Forwarder/RequestUtilities.cs#L419-L423)). The body is empty.
- The sign-out after a failed refresh only happens for `User` and `UserOrClient`, and only if `RemoveSessionAfterRefreshTokenExpiration=true` (the default, [BFF/Configuration/BffOptions.cs#L141](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Configuration/BffOptions.cs#L141)). It does not happen for our `UserOrNone`.
- The token type compare is case-insensitive, because some IdPs return `dpop` or `bearer` in other casing ([BFF/HttpContextExtensions.cs#L79-L82](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/HttpContextExtensions.cs#L79-L82)).
- An exception in the transform is logged and thrown again. YARP then returns 502.
- The forwarder `HttpMessageInvoker` comes from `ForwarderHttpClientFactory` (no cookies, no redirects, no automatic decompression). It does not use `IHttpClientFactory`, so the Aspire service-defaults resilience handler does not apply.

### 7.3 Local API (`/local-api/hello`)

`AsBffApiEndpoint()` adds only the metadata. Thus the endpoint gets the `X-CSRF` check and the 302 rules. No token logic applies.

## 8. Other implicit services

- License: with no key, `LicenseValidator` logs an error one time, and the cookie `OnSigningIn` counts unique `sub` values. Above 5, it logs a trial-mode error on each sign-in. Nothing is blocked ([BFF/Licensing/LicenseValidator.cs#L17-L36](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Licensing/LicenseValidator.cs#L17-L36), [BFF/SessionManagement/Configuration/PostConfigureApplicationCookieTrialModeCheck.cs#L25-L57](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/SessionManagement/Configuration/PostConfigureApplicationCookieTrialModeCheck.cs#L25-L57)). Drop it.
- Diagnostics summary: a hosted service logs server, assembly, and frontend data every hour and at shutdown ([BFF/Diagnostics/DiagnosticHostedService.cs#L17-L48](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Diagnostics/DiagnosticHostedService.cs#L17-L48), [BFF/Configuration/DiagnosticsOptions.cs#L15](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Configuration/DiagnosticsOptions.cs#L15)). Drop it.
- Metrics: meter `Duende.Bff` with `session.started` and `session.ended`. Only the server-side ticket store increments them, so they stay at zero for us ([BFF/Otel/BffMetrics.cs#L10-L21](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Otel/BffMetrics.cs#L10-L21)).
- `AddHttpContextAccessor()` and `AddAuthentication()`.
- An Ajax request to a UI endpoint (login, logout) only logs at Debug level. It is not blocked ([BFF/Endpoints/Internal/BffAntiForgeryMiddleware.cs#L55-L59](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/Endpoints/Internal/BffAntiForgeryMiddleware.cs#L55-L59)).
- Error responses of the management endpoints are `HttpValidationProblemDetails` with status 400 ([BFF/HttpContextExtensions.cs#L17-L29](https://github.com/DuendeSoftware/products/blob/bff-4.0.1/bff/src/Bff/HttpContextExtensions.cs#L17-L29)).

## 9. The keep list

Keep (the frontend or a security property needs it):

1. `/bff/login`: `returnUrl` local URL rule, 400 problem on a bad value, default `/`, `prompt` pass-through with the `prompt_values_supported` check, and redirect to `returnUrl` when the IdP returns an error for a prompt login.
2. `/bff/logout`: `sid` match for a session with `sid`, local `returnUrl`, cookie sign-out, refresh token revocation (configurable, and safe when `revocation_endpoint` is missing), RP-initiated logout.
3. `/bff/user`: `X-CSRF` check, 401 empty body for anonymous, claim array `{type, value, valueType}`, `bff:logout_url` with `sid`, `bff:session_expires_in` as a number, `bff:session_state` when present. Decide about `?slide=false`.
4. `X-CSRF: 1` on `/bff/user`, local APIs, and `/api/*`, for every method. 401 with an empty body on failure.
5. 302 to 401 and 302 to 403 on API endpoints, with `Location` and `Set-Cookie` removed.
6. Cookie: `__Host-bff`, `HttpOnly`, `SameSite=Lax`, `SecurePolicy=SameAsRequest`, 14 days sliding, chunking, tokens in the ticket.
7. Proxy: `/api/{**catch-all}` to `<server>/api/...`, `Cookie` removed, `X-Forwarded-*` set, user token attached, the `UserOrNone` semantics, a 401 short-circuit for a required token, refreshed cookie kept in the proxied response.
8. Token management: one call to `AddOpenIdConnectAccessTokenManagement`, `RefreshBeforeExpiration` from config, in-process refresh de-duplication.
9. Default schemes: cookie for authenticate and forbid, OIDC for challenge and sign-out.
10. The early handling of `/signin-oidc` and `/signout-callback-oidc`, or a plain `UseAuthentication` that gives the same result.

Candidates to drop on purpose (the spec must say so):

- `/bff/silent-login` and `/bff/silent-login-callback` (obsolete, not used).
- `/bff/diagnostics` (shows raw tokens).
- The license check, the diagnostics summary log, the `Duende.Bff` meter, `AddHybridCache`, and the multi-frontend machinery.
- The second `UseForwardedHeaders` from the startup filter.

## Gaps and open questions

1. **Back-channel logout does nothing today.** `/bff/backchannel` validates the token and returns 200, but `NopSessionRevocationService` ends no session. The compliance set has Back-Channel Logout 1.0 as a receiver. That needs a server-side session store (or a revocation list keyed by `sub` and `sid` that the cookie validation checks). The validation also needs the `typ=logout+jwt` check, `iat`, and `jti` replay protection. This belongs to the session storage decision.
2. **A failed refresh with `UserOrNone` leaves a broken session.** The BFF forwards with no token, the API returns 401, and `/bff/user` still says "signed in". Each later API call tries the refresh again and hits the token endpoint again. The spec must decide: sign out (as `User` does), return 401 from the BFF, or keep today's behavior.
3. **The id token can be lost after a refresh.** ATM writes `null` to `.Token.id_token` when the refresh response has no `id_token`. The Microsoft handler then sends no `id_token_hint` on logout. Some IdPs then show a confirmation page or reject `post_logout_redirect_uri`. This needs a test per provider. The spec must say to keep the old id token.
4. **The browser `Authorization` header passes through.** For an anonymous user, or after a failed refresh, the proxy forwards an `Authorization` header that the browser sent. The spec must decide to strip it. A BFF browser client must not send tokens.
5. **Two client authentication styles.** The code exchange sends the secret in the form body (Microsoft handler). The refresh and the revocation use HTTP Basic (ATM default). The provider capability matrix must check that each IdP accepts both, or the spec must pick one.
6. **PAR is already on.** The Microsoft OIDC handler in .NET 9 and later uses PAR when discovery has `pushed_authorization_request_endpoint` (`UseIfAvailable`). This is not a Duende behavior, but the map says PAR is not in phase 1. The spec must choose: keep `UseIfAvailable` or set `Disable` for phase 1.
7. **The diagnostics endpoint shows tokens in Development.** Any page on the same origin can read the user access token from `/bff/diagnostics`. Drop it, or keep it behind a flag.
8. **Refresh de-duplication is per process.** With more than one BFF replica, two replicas can refresh the same refresh token at the same time. With refresh token rotation, one of them fails. This belongs to the token refresh design.
