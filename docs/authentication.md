# Authentication

The app uses the Backend-For-Frontend (BFF) pattern. The browser never holds a token. `FullstackTemplate.Bff` holds the tokens and sends API calls to the server with a JWT. The BFF uses the ASP.NET Core OpenID Connect handler for login, `Duende.AccessTokenManagement.OpenIdConnect` for the token refresh and the revocation, and YARP for the proxy.

The design is in [docs/specs/home-rolled-bff.md](specs/home-rolled-bff.md). The decisions are in [docs/adr/](adr/).

## Endpoints

| Path | Method | `X-CSRF: 1` | Content |
|---|---|---|---|
| `/bff/login` | any | no | Starts a login |
| `/bff/logout` | GET | no | Ends the BFF session and starts the logout at the provider |
| `/bff/user` | GET | yes | The claims of the BFF session, or 401 |
| `/bff/backchannel` | POST | no | The back-channel logout receiver. Only the provider calls it. |
| `/signin-oidc` | GET | no | The login callback |
| `/signout-callback-oidc` | GET | no | The return from the logout at the provider |
| `/api/{**catch-all}` | any | yes | The proxy to the server |
| `/health`, `/alive` | GET | no | The health checks |

## Login flow

1. The user clicks Login in the React app.
2. The frontend goes to `/bff/login`. A `returnUrl` must be a local URL, for example `/weather`. The BFF rejects an absolute URL and a URL that starts with `//`.
3. The BFF sends the browser to the provider with the authorization code flow, PKCE (`S256`), and `response_mode=query`. If the provider has PAR, the BFF first pushes the request, and the authorize URL has only `client_id` and `request_uri`.
4. The provider sends the browser back to `/signin-oidc`. The BFF accepts only a GET there.
5. The BFF checks the callback. The `iss` parameter must equal the issuer of the provider (RFC 9207). The ID token `aud` must hold only the client id. A failed check gives a 400, and the BFF creates no BFF session.
6. The BFF redeems the code at the token endpoint and reads the user info.
7. The BFF writes a BFF session row in `bffdb`, and sets the `__Host-bff` cookie. The cookie holds only the key of the row.
8. The React app calls `/bff/user` with the `X-CSRF: 1` header to get the claims. The claims include `bff:logout_url`.
9. API calls go through the BFF at `/api/v1/*` with the `X-CSRF: 1` header. The BFF adds the access token of the BFF session.

The BFF builds `redirect_uri` and `post_logout_redirect_uri` from `Bff:PublicOrigin`, never from the request. Register these URLs at the provider:

| URL | Use |
|---|---|
| `{Bff:PublicOrigin}/signin-oidc` | The redirect URI |
| `{Bff:PublicOrigin}/signout-callback-oidc` | The post logout redirect URI |
| `{Bff:PublicOrigin}/bff/backchannel` | The back-channel logout URI, if the provider has back-channel logout |

### Cookies

| Cookie | Settings | Content |
|---|---|---|
| `__Host-bff` | `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/`, no `Domain` | The key of the BFF session row, encrypted with Data Protection |
| The correlation and nonce cookies | `SameSite=Lax` | The login state. The browser sends them on the redirect back from the provider. |

### Host trust

The BFF reads `X-Forwarded-For` and `X-Forwarded-Proto`. It does not read `X-Forwarded-Host`. The host filter accepts the host of `Bff:PublicOrigin` and the hosts in `Bff:AdditionalAllowedHosts`, and it gives a 400 for other hosts. In the local stack, the AppHost adds the container host of the BFF, `host.docker.internal`, and `localhost`.

The BFF accepts `X-Forwarded-For` and `X-Forwarded-Proto` from any source, because the address of the proxy in front of the BFF is not known. Thus a client can set its own client address. Today only the telemetry uses the client address: the request logs and the request spans. Before a deployment behind a proxy uses the client address for a decision, for example a rate limit or an IP allow list, set `KnownProxies` or `KnownIPNetworks` of `ForwardedHeadersOptions` in `FullstackTemplate.Bff/HostTrust/HostTrustExtensions.cs` to the proxy.

## BFF sessions

The BFF keeps each BFF session as one row in the `bffdb` database. The row holds the tokens, encrypted with Data Protection. The Data Protection keys, the refresh locks, and the replay cache of the logout tokens are also in `bffdb`. Thus many BFF instances can share one `bffdb`, and a deploy does not log out the users.

The AppHost adds `bffdb` on the same PostgreSQL server as `appdb`. The BFF applies its migrations at startup.

| Setting | Default | Content |
|---|---|---|
| `Bff:Session:SlidingExpiration` | 14 days | The BFF session ends after this time with no request. A request after half of this time renews it. |
| `Bff:Session:AbsoluteLifetime` | 30 days | The BFF session ends after this time from the login. A renewal does not move this limit. |
| `Bff:Session:CleanupInterval` | 10 minutes | The interval at which each BFF instance deletes the expired rows. |
| `Bff:Refresh:LockTimeout` | 10 seconds | The longest wait for the refresh lock of a BFF session. |
| `Bff:Refresh:TokenRequestTimeout` | 5 seconds | The longest refresh call to the token endpoint. It must be less than `Bff:Refresh:LockTimeout`. |

### Token refresh

When the access token expires, the BFF refreshes it before it sends the API call. A PostgreSQL advisory lock for each BFF session makes sure that only one request on all the BFF instances refreshes the token. The other requests wait and then use the new token.

- If the provider returns `invalid_grant`, or the refresh response has an ID token that fails the checks, the BFF ends the BFF session. The API call goes with no token.
- If the refresh fails for another reason, the token endpoint does not answer in time, or the lock is not free in time, the BFF session stays. The call goes with the current token if it is still valid, or with no token. The next call tries the refresh again.

## Logout flow

1. The frontend goes to the `bff:logout_url` of `/bff/user`, with a local `returnUrl`. The URL holds the `sid` of the BFF session, and the BFF rejects another `sid`.
2. The BFF deletes the BFF session row.
3. If the provider has a revocation endpoint, the BFF revokes the refresh token (RFC 7009). An error from the provider does not stop the logout.
4. The BFF deletes the `__Host-bff` cookie.
5. If the provider has an `end_session_endpoint`, the BFF sends the browser there with `id_token_hint` and `post_logout_redirect_uri`. The provider ends its session and sends the browser to `/signout-callback-oidc`. The BFF then sends the browser to `returnUrl`.
6. If the provider has no `end_session_endpoint`, the BFF sends the browser to `returnUrl`.

## Back-channel logout

When a user session ends at the provider, the provider sends a logout token to `POST /bff/backchannel` (OpenID Connect Back-Channel Logout 1.0). This occurs, for example, when an admin ends the session.

1. The BFF validates the token: the signature, `iss`, `aud`, `iat`, `exp`, the `typ` if present, the `events` claim, no `nonce`, and a `sub` or a `sid`.
2. The BFF rejects a `jti` that it has already seen.
3. The BFF deletes the BFF sessions with the `sid` of the token. If the token has only a `sub`, the BFF deletes all the BFF sessions of that `sub`.
4. The BFF returns 200. A token that fails a check gets a 400.

The BFF does not revoke the refresh tokens of the deleted BFF sessions, because the provider already ended its session. With Keycloak, a revoke of one refresh token also ends the other sessions of the user for the client, and Keycloak sends no logout token for them.

The endpoint is always mapped. The BFF has no front-channel logout endpoint, so `/signout-oidc` does not end a BFF session.

In the local stack, the Keycloak and Authentik seeds register the back-channel URL. The provider containers call the BFF through the Aspire container network.

## Provider features

The BFF reads the discovery document of the provider one time at startup. It uses each optional feature from this snapshot. A change of the provider features needs a restart of the BFF. Each feature has a setting under `Auth:Features`:

| Setting | Discovery field | With `Auto`, if the field is missing |
|---|---|---|
| `Auth:Features:Revocation` | `revocation_endpoint` | The BFF does not revoke tokens. |
| `Auth:Features:IssuerParameter` | `authorization_response_iss_parameter_supported` | The BFF accepts a callback with no `iss`. A present `iss` must still match. |
| `Auth:Features:BackChannelLogout` | `backchannel_logout_supported` | The endpoint stays mapped, and it validates each token. |
| `Auth:Features:EndSession` | `end_session_endpoint` | Logout sends the browser to the local `returnUrl`. |
| `Auth:Features:PushedAuthorization` | `pushed_authorization_request_endpoint` | The BFF uses the plain authorize URL. |
| `Auth:Features:DPoP` | `dpop_signing_alg_values_supported` | The BFF uses Bearer tokens. See [DPoP](#dpop). |

The values are:

- `Auto` (the default, except DPoP): the BFF uses the feature if discovery lists it. The BFF logs one warning for each missing feature.
- `Require`: the BFF stops at startup if discovery does not list the feature.
- `Disable`: the BFF never uses the feature.

These are the active features for each provider with `Auto`. The e2e tests have the same table, and one test makes sure that live discovery matches it.

| Feature | Keycloak | FusionAuth | Authentik | Duende Demo | Cognito | ZeroFactor |
|---|---|---|---|---|---|---|
| Revocation | yes | no | yes | yes | no | yes (a no-op) |
| `iss` parameter | yes | yes | no | yes | no | yes |
| Back-channel logout | yes | no | yes | yes (the demo cannot reach the local BFF, so no test calls the receiver) | no | no |
| `end_session_endpoint` | yes | yes | yes | yes | no (the BFF uses `/cognito-idp/logout`) | yes |
| PAR | yes | no | no | yes | no | no |
| Refresh token rotation | yes | yes | yes | no | no | no (the old token stays valid) |
| DPoP | yes (the stack starts it with `--features=dpop`) | listed, not tested | listed, not tested | yes | no | no |

Other settings:

| Setting | Default | Content |
|---|---|---|
| `Auth:ClientAuthentication` | `Auto` | `Auto`, `Basic`, or `Post`. `Auto` uses `client_secret_basic`, or `client_secret_post` if discovery lists only that method. |
| `Auth:IdTokenSigningAlgorithm` | `RS256` | The signing algorithm of the ID tokens and the logout tokens, as the client registration sets it. |
| `Auth:MetadataRefreshInterval` | The handler default | The shortest time between two reads of the discovery document and the signing keys. |
| `Auth:UseAudienceAsScope` | `true` | Adds `Auth:Audience` to the scopes. FusionAuth needs `false`. |
| `Auth:DefaultAccessTokenLifetime` | none | The lifetime of an access token when the token response has no `expires_in`. With no value, such a token never expires, and the BFF never refreshes it. |

## DPoP

DPoP (RFC 9449) binds the access token and the refresh token to a key of the BFF. A thief of a token cannot use it without the key. `Auth:Features:DPoP` is `Disable` by default, so the tokens are Bearer tokens.

| Setting | Content |
|---|---|
| `Auth:Features:DPoP` | `Disable` (the default), `Auto`, or `Require`. `Auto` uses DPoP if the key is set and discovery lists its algorithm. Otherwise the BFF logs one warning and uses Bearer tokens. `Require` stops the BFF at startup if the key is missing, or if discovery does not list its algorithm. |
| `Auth:DPoPJsonWebKey` | The one DPoP key of the BFF, as a private JSON Web Key with an `alg`, for example an EC P-256 key with `"alg": "ES256"`. It is a secret. |

How the BFF uses the key:

- The authorize request has `dpop_jkt`, the thumbprint of the key. The code exchange and each refresh have a DPoP proof.
- An API call has `Authorization: DPoP <token>` and a new `DPoP` proof for that request. The proof has the method, the URL of the API without the query, and the hash of the token.
- If the API answers `401` with `use_dpop_nonce`, the BFF keeps the nonce and sends the request one more time with a new proof. A request body larger than 64 KiB gets no retry, but the next request has the nonce.
- The BFF stores the key in the session ticket with the tokens, so the tokens of a session always match the key that bound them. A change of the key applies to new logins. A session from before the change has Bearer tokens, or the old key, until the user logs in again.

The server accepts both token types. A token with a `cnf.jkt` claim must come with `Authorization: DPoP` and a valid proof. The same token as a Bearer token gets `401`. The server checks each rule of RFC 9449 section 4.3 and section 7, and answers with `WWW-Authenticate: DPoP error="invalid_dpop_proof"` or `error="invalid_token"`. The proof of a request can pass one time: the server keeps the `jti` of each proof in the table `used_dpop_proofs` of `appdb` until the proof is too old to pass the `iat` check, and a background service deletes the expired rows. The server builds the URL of the proof from the scheme, host, path base, and path of the request. The server has no forwarded headers middleware. If a proxy between the BFF and the server ends TLS or changes the `Host` header, the URL of the proof does not match. Add `UseForwardedHeaders` to the server in that case.

| Server setting | Default | Content |
|---|---|---|
| `Auth:DPoP:ProofLifetime` | 60 s | The oldest `iat` that a proof can have. |
| `Auth:DPoP:ClockSkew` | 5 s | How far in the future an `iat` can be. |
| `Auth:DPoP:CleanupInterval` | 5 min | How often the server deletes expired replay rows. |

The server tags the span of a DPoP request with `app.auth.scheme=DPoP` and `app.dpop.jkt_prefix`, the first 8 characters of the thumbprint. No log or tag has a token, a proof, or a key.

To run the local stack with DPoP, set `DPoP=true`. The AppHost then sets `Auth:Features:DPoP` to `Require` and gives the BFF a generated EC P-256 key as the secret parameter `bff-dpop-key`. Only a provider whose class sets `TestedWithDPoP` can run with DPoP: the Duende Demo and Keycloak. Keycloak always starts with `--features=dpop`. Keycloak 26.0.8 has DPoP as a preview feature. FusionAuth and Authentik list DPoP in discovery, but this stack does not test DPoP with them. Cognito and ZeroFactor have no DPoP support. For another provider, the AppHost stops.

```bash
AuthProvider=Keycloak DPoP=true dotnet aspire start --isolated --non-interactive --apphost FullstackTemplate.AppHost
```

When the stack runs with DPoP, `run-against-stack.ts` sets `DPOP=true`, and the e2e test "should call the secure weather API with a DPoP-bound token" reads the server span of the secure weather call with `dotnet aspire otel spans`. Without DPoP, that test is skipped.

## Auth provider

The AppHost holds all provider config. The BFF and the server have no provider code. They read the `Auth:*` settings that the AppHost gives them, and the BFF reads the discovery document of the provider for the optional features ([ADR 0005](adr/0005-provider-features-by-discovery.md)). See [ADR 0007](adr/0007-regular-repo-instead-of-template.md).

### Select a provider

The `AuthProvider` setting in `FullstackTemplate.AppHost/appsettings.json` selects the provider at runtime. Its value is the name of a folder in `FullstackTemplate.AppHost/AuthProviders/`, without case sensitivity. The setting has no fallback: if it is empty or not known, the AppHost stops and lists the known providers.

- To change the default, run `dotnet run scripts/init.cs --auth-provider <Name>`, or edit the setting.
- To use another provider for one run, set the environment variable. For example:

  ```bash
  AuthProvider=Keycloak dotnet aspire start --isolated --non-interactive --apphost FullstackTemplate.AppHost
  ```

- To use another provider for one run, you can also give the setting as an AppHost argument after `--`:

  ```bash
  dotnet aspire run -- --AuthProvider ZeroFactor
  ```

The `ZeroFactor` provider is [0factor](https://github.com/c4l3b/0factor), a mock OIDC provider with no passwords. Its login page lists the test users, and one click signs in. It is for local development only.

### Change the provider of a database that has users

The app finds a user by the `sub` claim, which it stores in `User.Identifier`. Each provider gives its own `sub`. If you change the provider of a normal run, the database volume keeps the users of the old provider. Then a login with the new provider does not find the old user.

An isolated run starts with an empty database, so it does not have this problem.

- The Keycloak seed and the ZeroFactor seed give the test users the same IDs. You can change between these two providers and keep your data.
- To use the user of another provider with ZeroFactor, register a user on the ZeroFactor login page. Enter the `sub` of the old user as the User ID.

### Add a provider

1. Add the folder `FullstackTemplate.AppHost/AuthProviders/<Name>/`.
2. In the folder, add the class `<Name>AuthProvider` that implements `IAuthProvider`. The AppHost finds the class by reflection, and `<Name>` from the class name is the value of the `AuthProvider` setting. `AddTo` adds the containers of the provider to the stack and returns an `AuthProviderConfig`.
3. Put the seed files in `seed/`, and mount them with `WithSeedFiles`. Call `WithClientUrls` on the container, and read the app URLs from the `APP_*` variables in the seed files. An isolated run gives the apps random ports.
4. Add `frontend/e2e/idps/<name>.json`, with `<name>` in lowercase. Give the login form and the test user. Set `resource` to the name of the Aspire resource of the provider. Set `capabilities` to what the discovery document of the provider lists, and `sessionFeatures` to what the provider does. The e2e tests skip a feature test if the provider does not have the feature.
5. To run the provider with `DPoP=true`, make `TestedWithDPoP` of the class return true, and run the e2e tests with DPoP. See [DPoP](#dpop).

A unit test in `AuthProviderCatalogTests` makes sure that each provider has its folder and its e2e file.

### Delete a provider

1. Delete the folder `FullstackTemplate.AppHost/AuthProviders/<Name>/`.
2. Delete `frontend/e2e/idps/<name>.json`.
3. If the `AuthProvider` setting names the deleted provider, select another provider.

`AuthProviderConfig` holds each setting that can differ between providers, with the reason in its doc comments. The doc comments of each provider class give the differences of that provider and its test users.

## E2E tests

The Playwright tests in `frontend/e2e/auth-flows.spec.ts` log in through the React app and through Swagger UI. They run against the provider of the running stack. A test for a feature runs only for a provider that has the feature. For the other providers, a test proves the fallback.

1. Start the stack, as shown in [Select a provider](#select-a-provider). For the BFF refresh test and the refresh lock test, also set `BffRefreshBeforeExpirationSeconds`, as the CI e2e job does:

   ```bash
   AuthProvider=Keycloak BffRefreshBeforeExpirationSeconds=86400 dotnet aspire start --isolated --non-interactive --apphost FullstackTemplate.AppHost
   ```

   The AppHost gives the value to the BFF as `Auth:RefreshBeforeExpirationSeconds`. It is longer than the access token lifetime of each provider, so each API call makes the BFF refresh. Do not set it for a normal run. Without it, the BFF refresh test and the refresh lock test are skipped.
2. Wait until the server, the BFF, and the webfrontend are healthy:

   ```bash
   dotnet aspire wait server --status healthy --apphost FullstackTemplate.AppHost
   dotnet aspire wait bff --status healthy --apphost FullstackTemplate.AppHost
   dotnet aspire wait webfrontend --status healthy --apphost FullstackTemplate.AppHost
   ```

3. In `frontend`, run the tests for the provider:

   ```bash
   pnpm test:e2e:stack
   ```

The script `frontend/e2e/run-against-stack.ts` reads the stack from `dotnet aspire describe`. Then it sets these variables and runs `playwright test`:

| Variable | Value |
|---|---|
| `BASE_URL` | The `http` URL of `webfrontend` |
| `SERVER_URL` | The `https` URL of `server` |
| `AUTHORITY` | The `Auth__Authority` of the BFF. The live discovery test reads the discovery document from it, and skips when it is not set. |
| `IDP` | The name of a file in `frontend/e2e/idps/`. The default is the IdP whose `resource` is in the stack, or the IdP with no `resource` if the stack has none. Set it to override the IdP. |

Extra arguments go to Playwright, for example `pnpm test:e2e:stack --repeat-each=5`.

CI also sets an `EXPECT_*` variable for each feature that a leg must test, for example `EXPECT_REFRESH_MARGIN=true`. The script stops with an error when the variable and the stack disagree. Thus a leg cannot pass while its main test skips. An unset variable checks nothing.

The script also gives the tests the client id and the client secret of the BFF, and the admin credentials of the provider. It never prints them.

- The refresh rotation test uses the client of the BFF for its own code flow. It proves that the provider rejects the old refresh token after one refresh. It runs if `sessionFeatures.refreshRotation` is true in the IdP file: Keycloak, FusionAuth, and Authentik.
- The BFF refresh test makes two API calls through the BFF, and each call refreshes. The second call succeeds only if the BFF stored the rotated refresh token of the first call. Then the test logs out, and the `id_token_hint` proves that the BFF stored the ID token of the second refresh. It runs if `sessionFeatures.refreshRotation` is true in the IdP file: Keycloak, FusionAuth, and Authentik. On a provider that does not reject a reused refresh token, the test cannot prove the rotation.
- The refresh lock test makes two API calls through the BFF at the same time, and both need the same refresh. Both calls must succeed, and the BFF session must still work after them. With no refresh lock, both calls send the same refresh token, and the provider rejects the second one. It runs on the same providers as the BFF refresh test.
- The back-channel logout test ends the session at the provider through its admin API, and expects a 401 from `/bff/user`. It runs if `sessionFeatures.backChannelLogoutReachesBff` is true in the IdP file: on Keycloak, with the bootstrap admin, and on Authentik, with the `authentik-bootstrap-token` parameter of the AppHost. Authentik creates this token only on the first start of its database.

## Conformance

The OpenID Foundation conformance suite tests the BFF as a relying party. In this repository, `pnpm test:conformance` in `frontend` runs these five test plans against a local stack:

| Plan | Result |
|---|---|
| `oidcc-client-basic-certification-test-plan` | Passes, with one accepted warning |
| `oidcc-client-config-certification-test-plan` | Passes |
| `oidcc-client-rp-initiated-logout-rp-basic` | Passes |
| `oidcc-client-back-channel-logout-rp-basic` | Passes |
| `oidcc-client-refreshtoken-test-plan` | Passes. It is not part of the certification program. The BFF does not call the user info endpoint after a refresh, but the server does, because it checks the opaque access tokens of the suite there. |

[`frontend/conformance/accepted-results.json`](https://github.com/pdevito3/FullstackTemplate/blob/main/frontend/conformance/accepted-results.json) lists each accepted warning with its reason. The header of [`frontend/conformance/run-conformance.ts`](https://github.com/pdevito3/FullstackTemplate/blob/main/frontend/conformance/run-conformance.ts) tells how to start the stack and the suite. The `conformance` workflow of this repository runs the same plans. The `Conformance` provider of the AppHost, the `frontend/conformance` folder, the conformance scripts, and the `conformance` workflow are only in this repository. `scripts/init.cs` deletes them when it renames the app.

### Certify your own deployment

The OpenID Foundation certifies a deployment, not this repository. To certify your deployment, use the same plans with the hosted suite:

1. Deploy the app, so that the BFF has a public HTTPS URL.
2. Log in to the hosted suite at <https://www.certification.openid.net/>.
3. Create each plan of the table above, with these variants:
   - Client registration: `static_client`
   - Request type: `plain_http_request`
   - Client authentication: `client_secret_basic`, for all plans except the basic plan
   - Response mode: `default`, for all plans except the basic plan
4. Give each plan this configuration. Use one alias for all plans:

   ```json
   {
     "alias": "your-alias",
     "client": {
       "client_id": "your-client-id",
       "client_secret": "your-client-secret",
       "redirect_uri": "https://{bff}/signin-oidc",
       "post_logout_redirect_uri": "https://{bff}/signout-callback-oidc",
       "backchannel_logout_uri": "https://{bff}/bff/backchannel"
     }
   }
   ```

5. Set these BFF settings for the test run:
   - `Auth__Authority`: `https://www.certification.openid.net/test/a/your-alias/`
   - `Auth__ClientId` and `Auth__ClientSecret`: the values of step 4
   - `Auth__UseAudienceAsScope`: `false`
   - `Auth__MetadataRefreshInterval`: `00:00:01`, because each test module has new signing keys
6. For each test module, start the module, then restart the BFF. The suite gives the discovery document only while a module runs, and the BFF reads it at startup.
7. Do the login, or the login and the logout, in a browser. [`run-conformance.ts`](https://github.com/pdevito3/FullstackTemplate/blob/main/frontend/conformance/run-conformance.ts) of the FullstackTemplate repository shows the steps for each plan.
8. Export the results of each plan. Follow the instructions at <https://openid.net/certification/> to submit them.

Use a separate deployment for the test run. The suite is the identity provider of that deployment while the test runs.

## Test users

The seed files of each provider create its test users. The doc comment of each provider class in `FullstackTemplate.AppHost/AuthProviders/` lists them. The file in `frontend/e2e/idps/` gives the user that the e2e tests log in with.
