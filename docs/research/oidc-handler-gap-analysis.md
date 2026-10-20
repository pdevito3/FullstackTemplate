# Gap analysis: the Microsoft OIDC handler against the compliance set

Research for issue #8 of the map #6 "Migrate the BFF off Duende BFF".

## Question

For each rule in the compliance set, does the .NET 10 `Microsoft.AspNetCore.Authentication.OpenIdConnect` handler meet it by default, meet it with an option, or need our code? Also record the PAR and DPoP support of the handler for phase 2.

## Answer in short

The handler does the full code flow correctly: PKCE `S256`, a protected `state` bound to the browser, `nonce`, ID token validation, and the userinfo `sub` match. These are defaults or one-line options. The BFF must add its own code for four rules: RFC 9207 `iss` validation, RFC 7009 revocation (a call to `Duende.AccessTokenManagement`), Back-Channel Logout 1.0, and the BFF items of the browser-based apps draft (CSRF header, local-only return URLs, local sign-out with the IdP sign-out). Two smaller gaps also need our code: strict ID token audience checks and a guard that rejects `form_post` callbacks.

Two facts change phase 1:

- PAR is on by default. `PushedAuthorizationBehavior` defaults to `UseIfAvailable`. The `DuendeDemo` provider advertises a PAR endpoint, so the current BFF already sends PAR requests to it.
- The front-channel sign-out endpoint `/signout-oidc` is on by default. Front-Channel Logout is not in the compliance set.

## Sources

The handler source is the `release/10.0` branch of dotnet/aspnetcore at commit `a3bb8c0e`. The handler uses `Microsoft.IdentityModel.Protocols.OpenIdConnect` 8.19.2 (`IdentityModelVersion` in `eng/Versions.props`).

| Short name | Link |
|---|---|
| Handler | [OpenIdConnectHandler.cs](https://github.com/dotnet/aspnetcore/blob/a3bb8c0eac4ee390fa1f1a0d632ee427daf4e65f/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectHandler.cs) |
| Options | [OpenIdConnectOptions.cs](https://github.com/dotnet/aspnetcore/blob/a3bb8c0eac4ee390fa1f1a0d632ee427daf4e65f/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectOptions.cs) |
| PostConfigure | [OpenIdConnectPostConfigureOptions.cs](https://github.com/dotnet/aspnetcore/blob/a3bb8c0eac4ee390fa1f1a0d632ee427daf4e65f/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectPostConfigureOptions.cs) |
| Remote | [RemoteAuthenticationHandler.cs](https://github.com/dotnet/aspnetcore/blob/a3bb8c0eac4ee390fa1f1a0d632ee427daf4e65f/src/Security/Authentication/Core/src/RemoteAuthenticationHandler.cs), [RemoteAuthenticationOptions.cs](https://github.com/dotnet/aspnetcore/blob/a3bb8c0eac4ee390fa1f1a0d632ee427daf4e65f/src/Security/Authentication/Core/src/RemoteAuthenticationOptions.cs) |
| Cookies | [CookieAuthenticationOptions.cs](https://github.com/dotnet/aspnetcore/blob/a3bb8c0eac4ee390fa1f1a0d632ee427daf4e65f/src/Security/Authentication/Cookies/src/CookieAuthenticationOptions.cs) |
| Validator | [OpenIdConnectProtocolValidator.cs 8.19.2](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/8.19.2/src/Microsoft.IdentityModel.Protocols.OpenIdConnect/OpenIdConnectProtocolValidator.cs) |
| Metadata | [OpenIdConnectConfiguration.cs 8.19.2](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/8.19.2/src/Microsoft.IdentityModel.Protocols.OpenIdConnect/Configuration/OpenIdConnectConfiguration.cs) |
| OAuth 2.1 | [draft-ietf-oauth-v2-1-16](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16) (latest, September 2026) |
| BBA | [draft-ietf-oauth-browser-based-apps-27](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-browser-based-apps-27) (latest) |
| RFC 9207 | [OAuth 2.0 Authorization Server Issuer Identification](https://www.rfc-editor.org/rfc/rfc9207) |
| RFC 7009 | [OAuth 2.0 Token Revocation](https://www.rfc-editor.org/rfc/rfc7009) |
| RFC 9126 | [OAuth 2.0 Pushed Authorization Requests](https://www.rfc-editor.org/rfc/rfc9126) |
| RFC 9449 | [OAuth 2.0 Demonstrating Proof of Possession (DPoP)](https://www.rfc-editor.org/rfc/rfc9449) |
| Core | [OpenID Connect Core 1.0 (errata set 2)](https://openid.net/specs/openid-connect-core-1_0.html) |
| RPI | [OpenID Connect RP-Initiated Logout 1.0](https://openid.net/specs/openid-connect-rpinitiated-1_0.html) |
| BCL | [OpenID Connect Back-Channel Logout 1.0 (errata set 1)](https://openid.net/specs/openid-connect-backchannel-1_0.html) |
| Learn PAR | [What's new in ASP.NET Core 9.0: OpenIdConnectHandler adds support for PAR](https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-9.0) |

Line numbers in the evidence column refer to the files above.

## Status values

- **default**: the handler meets the rule with no configuration.
- **option**: the handler meets the rule when we set an option. The current value in `FullstackTemplate.Bff/AuthExtensions.cs` is given.
- **our code**: the handler does not meet the rule. The BFF must add code (an event handler, an endpoint, or middleware).

## Gap table

### OAuth 2.1 and OIDC Core: the authorization request and response

| Rule | Source section | Status | Evidence |
|---|---|---|---|
| Use the authorization code grant only. No implicit, no hybrid. | OAuth 2.1 [4.1](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16#section-4.1), [10.1](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16#section-10.1). BBA [6.1.3.1](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-browser-based-apps-27#section-6.1.3.1) | option | `ResponseType` defaults to `id_token` (Options L227). We set `ResponseType = "code"`. |
| Reject tokens in a query-mode response. | OAuth 2.1 [4.1.2](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16#section-4.1.2) | default | A GET callback that carries `id_token` or `access_token` fails with `UnexpectedParams` (Handler L642-653). |
| Accept only the response mode that we asked for. | OAuth 2.1 [4.1.2](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16#section-4.1.2) | our code (small) | `ResponseMode` defaults to `form_post` (Options L222). We set `query`. The handler still reads a POST form on the callback path (Handler L656-668). A forged POST still needs a valid `state` and correlation cookie, so the risk is low. A guard in `OnMessageReceived` that rejects non-GET callbacks makes "code flow only" strict. |
| PKCE with `S256`. No `plain`. | OAuth 2.1 [4.1.1](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16#section-4.1.1), [7.5.2](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16#section-7.5.2) | default | `UsePkce` defaults to `true` (Options L339). The handler makes a 32-byte random verifier and sends `code_challenge_method=S256` when `ResponseType` is `code` (Handler L410-425). It sends `code_verifier` on the token request (Handler L1268-1273). The handler has no `plain` path. We also set `UsePkce = true`. |
| Use `state`, bound to the user agent, one-time use. | OAuth 2.1 [2.3.3](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16#section-2.3.3), [7.10](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16#section-7.10). Core [3.1.2.1](https://openid.net/specs/openid-connect-core-1_0.html#AuthRequest) | default | `state` is the `AuthenticationProperties` encrypted with Data Protection (Handler L480, PostConfigure L45-50). It holds a correlation id. The matching correlation cookie is HttpOnly, `Secure` always, and is deleted on the callback (Remote L226-276, RemoteAuthenticationOptions L26-30). A missing or invalid `state` fails (Handler L693-726). The validator's own state compare is off (`RequireStateValidation = false`, Options L184-188) because the correlation check replaces it. |
| Send and check `nonce`. | Core [3.1.2.1](https://openid.net/specs/openid-connect-core-1_0.html#AuthRequest), [3.1.3.7](https://openid.net/specs/openid-connect-core-1_0.html#IDTokenValidation) | default | `RequireNonce = true` (Validator L71). The handler writes a nonce cookie (Handler L445-449, L1154-1164). It reads and deletes the cookie on the callback (Handler L857-864, L1173-1202). `ValidateTokenResponse` calls `ValidateNonce` (Validator L268-269). The nonce has a timestamp and a 15-minute lifetime (Options L187). |
| RFC 9207: compare the `iss` response parameter to the issuer, with simple string comparison. Reject a response without `iss` when the AS advertises `authorization_response_iss_parameter_supported`. Do not trust an error response that fails the check. | RFC 9207 [2.4](https://www.rfc-editor.org/rfc/rfc9207#section-2.4). OAuth 2.1 [4.1.2](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16#section-4.1.2), [7.15.1](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16#section-7.15.1) | our code | The handler reads `iss` only on the front-channel sign-out path (Handler L180-195). `HandleRemoteAuthenticateAsync` never reads `authorizationResponse.Iss` (Handler L629-946). The metadata model has `AuthorizationResponseIssParameterSupported` (Metadata L209), but the handler does not use it. Put the check in `OnMessageReceived`. That event runs before the correlation check and before the error branch (Handler L685, L729), so it also covers error responses. |
| Redirect URIs match exactly. | OAuth 2.1 [2.3.1](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16#section-2.3.1) | option | The AS does the exact match. The client side must send a fixed URI. The handler builds `redirect_uri` from the request scheme, host, path base, and `CallbackPath` (Handler L403). It saves the value and sends the same value on the token request (Handler L478, L1265). The URI is fixed only when the host is fixed. `Program.cs` trusts `X-Forwarded-Host` from all proxies. Register the exact URI at each provider and keep the host fixed (see open questions). |
| `redirect_uri` on the token request. | OAuth 2.1 [10.2](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16#section-10.2). Core [3.1.3.1](https://openid.net/specs/openid-connect-core-1_0.html#TokenRequest) | default | The handler sends it (Handler L1265). OAuth 2.1 removes the parameter, but section 10.2 tells the AS to accept it. OIDC Core 3.1.3.1 requires it. Keep the default. |
| Client authentication at the token endpoint. | OAuth 2.1 [2.4.1](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16#section-2.4.1), [3.2.1](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16#section-3.2.1). BBA [6.1.3.1](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-browser-based-apps-27#section-6.1.3.1) | default | The handler sends `client_id` and `client_secret` in the body (`client_secret_post`, Handler L1258-1266). OAuth 2.1 2.4.1 says the AS MUST support this method. `private_key_jwt` needs our code in `OnAuthorizationCodeReceived` (phase 2, BBA 6.1.3.7). |
| No open redirector after login or logout. | OAuth 2.1 [2.3.1](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16#section-2.3.1), [7.13.1](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16#section-7.13.1) | our code | The handler redirects to `AuthenticationProperties.RedirectUri` with no check (Remote L148, Handler L350-353). `/bff/login` and `/bff/logout` must accept only local return URLs. Duende BFF does this today. |

### OIDC Core: ID token and userinfo validation

| Rule | Source section | Status | Evidence |
|---|---|---|---|
| `iss` claim exactly matches the discovery issuer. | Core [3.1.3.7](https://openid.net/specs/openid-connect-core-1_0.html#IDTokenValidation) | default | The handler gives the configuration manager to the token handler, which validates `iss` against the discovery `issuer` (Handler L1437-1450). The validator also requires `iss` (Validator L368-369). |
| `aud` contains `client_id`. | Core 3.1.3.7 | default | PostConfigure sets `ValidAudience = ClientId` (PostConfigure L63-66). The validator requires `aud` (Validator L359-360). |
| Reject an ID token with audiences that the client does not trust. | Core 3.1.3.7 | our code (small) | `JsonWebTokenHandler` accepts a token if any audience matches. A token with more audiences passes. With many audiences and no `azp`, the validator only logs a warning (Validator L388-392). Add a check in `OnTokenValidated`. Do not set `ProtocolValidator.IdTokenValidator`, because a custom delegate turns off the default checks (Validator L341-353). |
| `azp`, when present, equals `client_id`. | Core 3.1.3.7 | default | Validator L394-405. |
| Signature: TLS replaces the signature check for an ID token from the token endpoint. Other ID tokens need a valid JWS signature with the issuer's keys. | Core 3.1.3.7 | default | For the token-endpoint ID token the handler sets `RequireSignedTokens = false` (Handler L837-839). The spec permits this ("the TLS server validation MAY be used ... in place of checking the token signature"). A signed token is still checked against the discovery keys, and keys refresh on rollover (`RefreshOnIssuerKeyNotFound`, Options L207). |
| `alg` is `RS256` or the registered algorithm. | Core 3.1.3.7 | option | Set `TokenValidationParameters.ValidAlgorithms` to the provider's algorithms if the spec wants a strict list. With `RequireSignedTokens = false`, an unsigned token from the token endpoint passes. This is permitted by the TLS rule above. A test confirms the behavior. |
| `exp` is in the future. `iat` is present. | Core 3.1.3.7 | default | `ValidateLifetime` is on by default in `TokenValidationParameters`. The validator requires `exp` and `iat` (Validator L362-366). |
| `sub` is present. Both ID tokens of a hybrid response have the same `sub`. | Core [2](https://openid.net/specs/openid-connect-core-1_0.html#IDToken) | default | `RequireSub = true` (Validator L372-373). Handler L880-883. |
| `acr` and `auth_time` checks. | Core 3.1.3.7 | option | `RequireAcr` and `RequireAuthTime` exist (Validator L136-148). The handler does not compare `auth_time` to `max_age`. We send no `acr_values` and no `max_age`, so no code is necessary now. |
| `at_hash`, when present, matches the access token. | Core [3.1.3.8](https://openid.net/specs/openid-connect-core-1_0.html#CodeFlowTokenValidation) | default | Validator L271-276. |
| Token response has `id_token` and `access_token`. | Core [3.1.3.5](https://openid.net/specs/openid-connect-core-1_0.html#TokenResponseValidation) | default | Validator L261-263. |
| Userinfo `sub` exactly matches the ID token `sub`. Else do not use the userinfo values. | Core [5.3.2](https://openid.net/specs/openid-connect-core-1_0.html#UserInfoResponse), [5.3.4](https://openid.net/specs/openid-connect-core-1_0.html#UserInfoResponseValidation) | option | The check runs only when `GetClaimsFromUserInfoEndpoint = true` (Options L145, Handler L906-909). We set it. `ValidateUserInfoResponse` compares `sub` and throws on a mismatch (Validator L319-326), before the claim actions run (Handler L1087-1098). |
| Signed userinfo (`application/jwt`): validate the signature. | Core 5.3.4 | our code (only if used) | The handler parses a JWT userinfo response with no signature check (Handler L1066-1070). We do not register signed userinfo, so no code is necessary now. |
| Metadata over HTTPS. | OAuth 2.1 [1.5](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16#section-1.5) | option | `RequireHttpsMetadata` defaults to `true` (Options L156, PostConfigure L95-98). We set it to `false` in Development only. |

### RP-Initiated Logout 1.0

| Rule | Source section | Status | Evidence |
|---|---|---|---|
| Redirect to `end_session_endpoint` with `id_token_hint` (RECOMMENDED), `post_logout_redirect_uri`, and `state`. | RPI [2](https://openid.net/specs/openid-connect-rpinitiated-1_0.html#RPLogout), [2.1](https://openid.net/specs/openid-connect-rpinitiated-1_0.html#OPMetadata) | default | `SignOutAsync` reads `end_session_endpoint` from discovery, adds `id_token_hint` from the saved tokens, sets `post_logout_redirect_uri` to `SignedOutCallbackPath`, and protects `state` (Handler L208-306). `id_token_hint` needs `SaveTokens = true`, which we set. `client_id` is not sent. RPI 2 says it is OPTIONAL. |
| `post_logout_redirect_uri` is registered and matches exactly. | RPI [3](https://openid.net/specs/openid-connect-rpinitiated-1_0.html#RedirectionAfterLogout) | option | The value is `SignedOutCallbackPath` (default `/signout-callback-oidc`, Options L45). Register it at each provider. |
| The post-logout callback goes to a safe local page. | RPI 3. OAuth 2.1 7.13.1 | default + our code | The callback unprotects `state` and redirects to the saved `RedirectUri` (Handler L312-356). A forged `state` fails to decrypt. The saved value comes from our `/bff/logout` endpoint, so that endpoint must accept only local URLs. |
| Sign out the local session, then sign out at the OP. Protect the logout endpoint against CSRF. | RPI 2. BBA [6.1.2.1](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-browser-based-apps-27#section-6.1.2.1), [6.1.3.3](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-browser-based-apps-27#section-6.1.3.3) | our code | The handler only builds the OP redirect. It does not clear the cookie. `/bff/logout` signs out the cookie scheme and the OIDC scheme. It checks the `sid` query value from the `bff:logout_url` claim, as Duende BFF does. The handler throws if the provider has no `end_session_endpoint` (Handler L270-273). |

### RFC 7009 revocation

| Rule | Source section | Status | Evidence |
|---|---|---|---|
| Revoke the refresh token on logout, with client authentication, at `revocation_endpoint`. | RFC 7009 [2.1](https://www.rfc-editor.org/rfc/rfc7009#section-2.1), [2.2](https://www.rfc-editor.org/rfc/rfc7009#section-2.2). BBA [6.1.2.3](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-browser-based-apps-27#section-6.1.2.3) | our code | The handler has no revocation code. `Duende.AccessTokenManagement.OpenIdConnect` 4.1.0 has `IUserTokenManager.RevokeRefreshTokenAsync` and `HttpContext.RevokeRefreshTokenAsync`. `/bff/logout` calls one of them before the sign-out when `Auth:RevokeRefreshTokenOnLogout` is `true`. Duende BFF does this today (`RevokeRefreshTokenOnLogout`). A failed revocation does not stop the logout. |

### Back-Channel Logout 1.0 (receiver)

| Rule | Source section | Status | Evidence |
|---|---|---|---|
| Expose a back-channel logout URI that takes a POST form with `logout_token`. | BCL [2.2](https://openid.net/specs/openid-connect-backchannel-1_0.html#BCRegistration), [2.5](https://openid.net/specs/openid-connect-backchannel-1_0.html#BCRequest) | our code | The handler has no back-channel endpoint. The feature request [dotnet/aspnetcore#4659](https://github.com/dotnet/aspnetcore/issues/4659) is open in the Backlog milestone. `RemoteSignOutPath` is front-channel logout, not back-channel (Handler L104-202). |
| Validate the logout token: signature and `alg` as for an ID token, no `none`. `iss`, `aud`, `iat`, `exp`. `sub` or `sid` present. `events` has `http://schemas.openid.net/event/backchannel-logout`. No `nonce`. Optional `jti` replay check. Use `typ` `logout+jwt` against cross-JWT confusion. | BCL [2.4](https://openid.net/specs/openid-connect-backchannel-1_0.html#LogoutToken), [2.6](https://openid.net/specs/openid-connect-backchannel-1_0.html#Validation), [4.1](https://openid.net/specs/openid-connect-backchannel-1_0.html#CrossJWT) | our code | Reuse `JsonWebTokenHandler` with the handler's `ConfigurationManager` and `ValidAudience = ClientId`, and set `RequireSignedTokens = true`. Then check `events`, `nonce`, `sub`/`sid`, and `typ` in our code. |
| Clear the sessions for `iss` + `sub` and/or `sid`. Respond 200 on success, 400 on a validation failure, with `Cache-Control: no-store`. | BCL [2.7](https://openid.net/specs/openid-connect-backchannel-1_0.html#BCActions), [2.8](https://openid.net/specs/openid-connect-backchannel-1_0.html#BCResponse) | our code | The request comes from the OP, not the browser, so the BFF cannot delete the cookie. The BFF needs a server-side session store (an `ITicketStore` for the cookie handler) that it can query by `sub` and `sid`. This ties the work to the session storage decision. |

### Browser-based apps draft, BFF section

| Rule | Source section | Status | Evidence |
|---|---|---|---|
| Confidential client, code grant. | BBA [6.1.3.1](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-browser-based-apps-27#section-6.1.3.1) | option | `ClientSecret` and `ResponseType = "code"` are set. |
| Session cookie: `Secure` (MUST). | BBA [6.1.3.2](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-browser-based-apps-27#section-6.1.3.2) | option | The cookie handler default is `SameAsRequest` (Cookies L21). We set `SameAsRequest`. Set `CookieSecurePolicy.Always`. Browsers treat `http://localhost` as a secure context, so local development keeps working. The `__Host-` prefix already forces `Secure` in the browser. |
| Session cookie: `HttpOnly` (MUST). | BBA 6.1.3.2 | default | Cookies L20. |
| Session cookie: `SameSite=Strict` (SHOULD). | BBA 6.1.3.2, [6.1.3.3.1](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-browser-based-apps-27#section-6.1.3.3.1) | option | The default is `Lax` (Cookies L19). We set `Lax` with the comment "required for OIDC redirects". The handler's correlation and nonce cookies carry the callback, not the session cookie. A decision ticket sets the value (see open questions). |
| Session cookie: path `/`, no `Domain`, `__Host-` prefix (SHOULD). | BBA 6.1.3.2 | option | `Cookie.Name = "__Host-bff"`. The path defaults to `/` and `Domain` is unset. The draft example prefix is `__Host-Http-`. The wire contract fixes `__Host-bff`, which meets the `__Host-` intent. |
| Encrypt the cookie contents (SHOULD, for client-side sessions with tokens). | BBA 6.1.3.2 | default | The cookie handler protects the ticket with Data Protection (authenticated encryption). |
| Correlation and nonce cookies. | BBA 6.1.3.2 | default + option | Both are HttpOnly, `Secure` always, `SameSite=None`, path = `CallbackPath` (RemoteAuthenticationOptions L26-30, Options L78-85). `None` is for `form_post`. With `response_mode=query`, `Lax` is enough. The names have no `__Host-` prefix and the path is not `/`. These are SHOULD items. |
| CSRF defense (MUST). A static custom header on every request (SHOULD). | BBA [6.1.3.3](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-browser-based-apps-27#section-6.1.3.3), [6.1.3.3.2](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-browser-based-apps-27#section-6.1.3.3.2) | our code | The handler has no CSRF code for the BFF endpoints. Middleware requires `X-CSRF: 1` on `/bff/user`, the logout call from the SPA, and the proxied API routes. It returns 401 for an anonymous user. No CORS policy approves other origins. Duende BFF does this today. |
| Proxy only to an allowlist of hosts and paths. | BBA [6.1.3.6](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-browser-based-apps-27#section-6.1.3.6) | our code (config) | Not a handler concern. YARP routes and clusters are static configuration. Keep them static. |
| Session lifetime tracks the refresh token. End the session when refresh fails. | BBA [6.1.2.2](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-browser-based-apps-27#section-6.1.2.2) | our code | Not a handler concern. It belongs to the token refresh design (map "Not yet specified"). |

### Phase 2 record: PAR and DPoP

| Feature | Source section | Status | Evidence |
|---|---|---|---|
| PAR (RFC 9126) | RFC 9126 [2](https://www.rfc-editor.org/rfc/rfc9126#section-2), [4](https://www.rfc-editor.org/rfc/rfc9126#section-4) | default (on) | Added in .NET 9 (Learn PAR). `PushedAuthorizationBehavior` defaults to `UseIfAvailable` (Options L419). The handler pushes when discovery has `pushed_authorization_request_endpoint`. It throws when the option is `Disable` but the AS sets `require_pushed_authorization_requests`. It throws when the option is `Require` but the AS has no PAR endpoint (Handler L488-519). It sends `client_secret` in the PAR body, or our code handles client authentication in `OnPushAuthorization` (Handler L552-602). Duende BFF 4.0.1 does not change this option. The `DuendeDemo` discovery document has a PAR endpoint and `require_pushed_authorization_requests: false` (checked 2026-10-20). So the current BFF already uses PAR with that provider. |
| DPoP (RFC 9449) | RFC 9449 [5](https://www.rfc-editor.org/rfc/rfc9449#section-5), [7](https://www.rfc-editor.org/rfc/rfc9449#section-7) | our code (library) | The handler and the JWT bearer handler have no DPoP code. The feature request [dotnet/aspnetcore#58016](https://github.com/dotnet/aspnetcore/issues/58016) is open. `Duende.AccessTokenManagement.OpenIdConnect` 4.1.0 has `UserTokenManagementOptions.DPoPJsonWebKey`. It hooks the OIDC handler's backchannel to add DPoP proofs to the code exchange (`AuthorizationServerDPoPHandler`, `ConfigureOpenIdConnectOptions`) and to the refresh and API calls. The server API needs its own DPoP proof validation, because `JwtBearer` has none. |

## Options to set in the new BFF

These are the handler and cookie options that the spec must lock. Most are already set in `AuthExtensions.cs`.

| Option | Value | Reason |
|---|---|---|
| `ResponseType` | `"code"` | Code flow only. The default is `id_token`. |
| `ResponseMode` | `"query"` | Matches the OAuth 2.1 default. The handler then omits the parameter (Handler L436-443). |
| `UsePkce` | `true` | Default. Keep it explicit. |
| `GetClaimsFromUserInfoEndpoint` | `true` | Turns on the userinfo `sub` check. |
| `SaveTokens` | `true` | Needed for `id_token_hint`, revocation, and the token manager. |
| `MapInboundClaims` | `false` | Keeps the JWT claim names. Not a compliance item. |
| `RequireHttpsMetadata` | `true` outside Development | Default is `true`. |
| `PushedAuthorizationBehavior` | Decision needed | The default `UseIfAvailable` already uses PAR. Phase 1 says PAR is out of scope. |
| `RemoteSignOutPath` | Decision needed | The default `/signout-oidc` turns on front-channel logout, which is not in the compliance set. Set it to an empty `PathString` to turn it off. |
| `CorrelationCookie.SameSite`, `NonceCookie.SameSite` | `Lax` | `None` is only for `form_post`. |
| Cookie `Name` | `__Host-bff` | Wire contract. |
| Cookie `SecurePolicy` | `Always` | BBA 6.1.3.2 MUST. The current value is `SameAsRequest`. |
| Cookie `SameSite` | Decision needed (`Strict` or `Lax`) | BBA 6.1.3.2 SHOULD `Strict`. Current value `Lax`. |
| `TokenValidationParameters.ValidAlgorithms` | The providers' algorithms (optional) | Strict `alg` list. |

## Code that the BFF must add

1. RFC 9207 `iss` check in `OnMessageReceived`. Compare with ordinal equality to the discovery `issuer`. Reject a missing `iss` when the AS sets `authorization_response_iss_parameter_supported`. Fail the request before the handler reads the error or the code.
2. A guard in `OnMessageReceived` that rejects a callback that is not a GET (strict `response_mode=query`).
3. A strict audience check in `OnTokenValidated`: `aud` holds only the client id or other trusted values.
4. `/bff/login`: local return URL check, then challenge.
5. `/bff/logout`: `sid` check, local return URL check, refresh token revocation through `Duende.AccessTokenManagement`, cookie sign-out, OIDC sign-out.
6. CSRF middleware for `X-CSRF: 1` with the 401 for an anonymous user.
7. Back-channel logout endpoint, logout token validation, and a server-side session store that it can query by `sub` and `sid`.

## Open questions for the map

- **PAR in phase 1.** The handler default already sends PAR requests to `DuendeDemo`, and to any other provider that advertises a PAR endpoint. Phase 1 says PAR is out of scope. The spec must choose: set `Disable` in phase 1 (the handler then fails for a provider that requires PAR), or accept `UseIfAvailable` and test PAR in phase 1. This needs a decision ticket.
- **Front-channel logout endpoint.** `/signout-oidc` is on by default. If the session has no `sid` claim, any top-level cross-site GET to it signs the user out. The `iss` check there never runs, because the default claim actions delete the `iss` claim (Options L60). The spec must turn it off or add Front-Channel Logout 1.0 to the compliance set.
- **Session cookie `SameSite`.** BBA 6.1.3.2 says `Strict`. The current value is `Lax`. With `Strict`, the browser does not send the session cookie on the first navigation that comes from the IdP redirect chain, or on a link from another site. A decision ticket weighs this against the SPA's first load.
- **Host trust for redirect URIs.** `Program.cs` trusts `X-Forwarded-Host` from any proxy. The handler builds `redirect_uri` and `post_logout_redirect_uri` from the host. The AS exact match stops a wrong host, but the spec must state a fixed host or an allowlist.
- **Back-channel logout needs server-side sessions.** This is an input to the session storage decision.
