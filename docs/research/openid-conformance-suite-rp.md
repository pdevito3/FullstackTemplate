# OpenID conformance suite for the BFF as a relying party

Research for issue #10 of the map "Migrate the BFF off Duende BFF" (#6). Researched on 2026-10-20 against the `master` branch of the suite.

## Question

Can the OpenID Foundation (OIDF) conformance suite test our BFF as a relying party (RP), and what does it cost to run?

## Answer

Yes. The suite has certifiable RP test plans for the code flow, RP-Initiated Logout, and Back-Channel Logout. It is MIT-licensed and runs locally from prebuilt Docker images, so a test run costs nothing. A run gives a test report. A certification is a separate, paid step: $700 for OIDF members or $3,500 for non-members per RP deployment per calendar year, unless OIDF grants an open-source fee waiver.

The suite acts as the OP. Each test waits until the RP starts a login, so a CI job needs a driver that creates each test through the suite API and then pushes a browser through `/bff/login` and `/bff/logout`. The suite ships no driver for a browser-based RP such as ours. We write our own.

The current BFF fails at least one test out of the box: the Basic plan accepts `client_secret_basic` only, and the Microsoft `OpenIdConnect` handler sends the secret in the POST body.

## How the suite tests an RP

- The suite is a "fake" OP. You point the RP at the suite instead of a real provider ([OIDF RP testing instructions](https://openid.net/certification/connect_rp_testing/)).
- You create a test plan, then run each test module in it. A new module goes to the `WAITING` state. Then you start the login flow in the RP ([OIDF RP testing instructions](https://openid.net/certification/connect_rp_testing/)).
- Each module expects a fixed sequence of requests and finishes when that sequence arrives. Negative tests wait for a timeout (5 seconds by default, `waitTimeoutSeconds`) and then finish ([OIDF RP testing instructions](https://openid.net/certification/connect_rp_testing/), [`AbstractOIDCCClientTest.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/client/AbstractOIDCCClientTest.java)).
- The suite authorization endpoint shows no login page. It sends a `RedirectView` straight back to the RP ([`AbstractOIDCCClientTest.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/client/AbstractOIDCCClientTest.java)). The driver does not need to type a user name or password.
- The issuer is `<BASE_URL>/test/a/<alias>/`. The alias is part of the plan configuration and of the redirect URI that the suite expects ([OIDF RP testing instructions](https://openid.net/certification/connect_rp_testing/), [`run-test-plan.py`](https://gitlab.com/openid/conformance-suite/-/blob/master/scripts/run-test-plan.py)).
- Each test module generates new server signing keys (`OIDCCGenerateServerJWKs`) and adds unusable keys to the published JWKS ([`AbstractOIDCCClientTest.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/client/AbstractOIDCCClientTest.java)).

## Which RP test plans apply

The plan names come from the `@PublishTestPlan` annotations in the suite source. Our BFF uses the code flow, a static client, a client secret, and discovery.

| Plan (`testPlanName`) | Certifiable | Certification profile | Applies to us | Reason |
|---|---|---|---|---|
| `oidcc-client-basic-certification-test-plan` | Yes | Basic RP | Yes | Code flow. OIDC Core for the RP is in our compliance set. |
| `oidcc-client-config-certification-test-plan` | Yes | Config RP | Yes | The BFF uses discovery. The plan tests discovery, issuer mismatch, and key rotation. |
| `oidcc-client-rp-initiated-logout-rp-basic` | Yes | RP-Initiated RP | Yes | RP-Initiated Logout 1.0 is in our compliance set. |
| `oidcc-client-back-channel-logout-rp-basic` | Yes | Back-Channel RP | Yes | Back-Channel Logout 1.0 as a receiver is in our compliance set. |
| `oidcc-client-refreshtoken-test-plan` | No | none | Yes, report only | The BFF refreshes tokens. The plan is "not currently part of certification program". |
| `oidcc-client-dynamic-certification-test-plan` | Yes | Dynamic RP | No | The Microsoft handler has no dynamic client registration. |
| Implicit, Hybrid, and Form Post plans | Yes | various | No | We use `response_type=code` and `response_mode=query` only. |
| Front-Channel Logout and Session Management plans | Yes | various | No | Not in our compliance set. |
| `oidcc-client-test-3rd-party-init-login-test-plan` | No | none | No | The BFF has no `initiate_login_uri`. |
| FAPI client plans | n/a | n/a | No | FAPI is out of scope. |

Sources: [`OIDCCClientBasicTestPlan.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/client/OIDCCClientBasicTestPlan.java), [`OIDCCClientConfigTestPlan.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/client/OIDCCClientConfigTestPlan.java), [`OIDCCClientRefreshTokenProfileTestPlan.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/client/OIDCCClientRefreshTokenProfileTestPlan.java), [`OIDCCClientRPInitiatedLogoutRPBasicTestPlan.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/client/logout/plan/OIDCCClientRPInitiatedLogoutRPBasicTestPlan.java), [`OIDCCClientBackChannelLogoutRPBasicTestPlan.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/client/logout/plan/OIDCCClientBackChannelLogoutRPBasicTestPlan.java), [plan folder](https://gitlab.com/openid/conformance-suite/-/tree/master/src/main/java/net/openid/conformance/openid/client).

### Test modules in each applicable plan

- **Basic RP** (fixed variants `response_type=code`, `response_mode=default`, `client_auth_type=client_secret_basic`): success case, invalid `iss`, missing `sub`, invalid `aud`, missing `iat`, `kid` absent with one key, `kid` absent with many matching keys, RS256 signature, `alg=none`, bad RS256 signature, bad `sub` in the UserInfo response, invalid `nonce`, UserInfo claims by scope, and `client_secret_basic` ([`OIDCCClientBasicTestPlan.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/client/OIDCCClientBasicTestPlan.java)).
- **Config RP**: discovery, `jwks_uri` keys, issuer mismatch in discovery, `alg=none`, key rotation just before signing, and key rotation ([`OIDCCClientConfigTestPlan.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/client/OIDCCClientConfigTestPlan.java)).
- **RP-Initiated RP**: RP-initiated logout, logout with a different `state` in the return, and logout without `state` ([`OIDCCClientRPInitiatedLogoutRPBasicTestPlan.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/client/logout/plan/OIDCCClientRPInitiatedLogoutRPBasicTestPlan.java)).
- **Back-Channel RP**: valid logout token, `alg=none`, no `events` claim, a `nonce` in the token, wrong `alg`, wrong `aud`, wrong `events`, and wrong `iss` ([`OIDCCClientBackChannelLogoutRPBasicTestPlan.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/client/logout/plan/OIDCCClientBackChannelLogoutRPBasicTestPlan.java)). Each test starts with RP-Initiated Logout. The suite then POSTs the logout token to the `backchannel_logout_uri` of the client ([`AbstractOIDCCClientBackChannelLogoutTest.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/client/logout/AbstractOIDCCClientBackChannelLogoutTest.java), [`CallRPBackChannelLogoutEndpoint.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/condition/as/logout/CallRPBackChannelLogoutEndpoint.java)). The negative tests require an HTTP 400 from the RP ([`OIDCCClientTestBackChannelLogoutWrongIssuer.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/client/logout/OIDCCClientTestBackChannelLogoutWrongIssuer.java)). The suite also checks the cache headers of the response.
- **Refresh token** (report only): valid refresh, refresh with an invalid `iss`, and refresh with an invalid `sub` ([`OIDCCClientRefreshTokenProfileTestPlan.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/client/OIDCCClientRefreshTokenProfileTestPlan.java)).

## Gaps in the current BFF that the suite finds

These items come from the suite source compared with our BFF code. A spike against a live suite must confirm each one.

1. **Client authentication.** The Basic plan fixes `client_auth_type=client_secret_basic`. The suite then advertises `client_secret_basic` only and validates the token request with `OIDCCValidateClientAuthenticationWithClientSecretBasic` ([`AbstractOIDCCClientTest.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/client/AbstractOIDCCClientTest.java)). The ASP.NET Core `OpenIdConnectHandler` puts `client_id` and `client_secret` in the form body of the code redemption (`client_secret_post`) ([`OpenIdConnectHandler.cs`](https://github.com/dotnet/aspnetcore/blob/main/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectHandler.cs)). Our BFF sets `ClientSecret` and changes nothing else (`FullstackTemplate.Bff/AuthExtensions.cs`). The BFF must send an HTTP Basic `Authorization` header, for example from the `OnAuthorizationCodeReceived` event. The token refresh path through `Duende.AccessTokenManagement` needs the same style.
2. **Signing key cache.** Each test module has new signing keys. The Microsoft handler caches the discovery document and JWKS through `ConfigurationManager`. The automatic refresh interval is 12 hours, and a forced refresh (`RequestRefresh`) waits at least 5 minutes by default ([`BaseConfigurationManager`](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.baseconfigurationmanager)). The handler calls `RequestRefresh` only after a `SecurityTokenSignatureKeyNotFoundException`, and that login fails ([`OpenIdConnectHandler.cs`](https://github.com/dotnet/aspnetcore/blob/main/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectHandler.cs)). So the second test of a run fails unless the BFF gets a fresh cache for each test. Options: restart the BFF between tests, or use a short `RefreshInterval` (minimum 1 second) in a conformance configuration.
3. **Back-channel logout endpoint.** Duende BFF supplies this endpoint today. The new BFF must supply it, reject bad logout tokens with HTTP 400, and send `Cache-Control: no-store`.

## Run the suite locally next to an isolated Aspire stack

The suite publishes prebuilt images. No source build is necessary ([Build and Run wiki](https://gitlab.com/openid/conformance-suite/-/wikis/Developers/Build-&-Run)).

1. Get the compose file: `curl -O https://gitlab.com/openid/conformance-suite/-/raw/master/docker-compose-prebuilt.yml`.
2. Start it: `docker compose -f docker-compose-prebuilt.yml up`. Set `IMAGE_TAG` to pin a version. The default tag `latest` tracks `master`.
3. Open `https://localhost.emobix.co.uk:8443/`. The hostname resolves to `127.0.0.1` in public DNS. The wiki also tells you to add it to the hosts file.

The stack has three containers: `mongodb`, `nginx` (TLS on 8443 and 8444, self-signed certificate), and `server` (Java) ([`docker-compose-prebuilt.yml`](https://gitlab.com/openid/conformance-suite/-/blob/master/docker-compose-prebuilt.yml)). The default Spring profile is `dev`, which turns off the UI login and the API token. The compose file keeps MongoDB data in `./mongo/data`. Delete that folder for a clean run.

The suite and the Aspire stack do not conflict. The suite uses fixed ports 8443 and 8444. An `--isolated` Aspire run uses random ports. One suite instance can serve many worktrees, because each test plan has its own alias.

## How the suite reaches the BFF

There are three paths. Each one has a different direction.

| Path | From | To | What it needs |
|---|---|---|---|
| Browser redirects (authorize, callback, end session, post-logout redirect) | Browser | Suite and BFF | The browser reaches both. `localhost` works. |
| Back-channel calls (discovery, JWKS, token, UserInfo) | BFF | Suite | The BFF must trust the self-signed `nginx` certificate. |
| Back-channel logout | Suite `server` container | BFF | The container must reach the BFF, for example at `host.docker.internal:<port>`. |

- **Redirect URI.** In our stack the Vite dev server proxies `/signin-oidc` to the BFF (`frontend/vite.config.ts`). So the redirect URI is `http://localhost:<vite port>/signin-oidc`. In an isolated run that port is random, so the driver reads it from the stack and writes it into the static client configuration (`client.redirect_uri`) of each plan. The suite accepts an `http` redirect URI for a web client with `response_type=code` ([`EnsureValidRedirectUriForAuthorizationEndpointRequest.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/condition/as/EnsureValidRedirectUriForAuthorizationEndpointRequest.java)). The logout plans also need `client.post_logout_redirect_uri` ([`AbstractOIDCCClientLogoutTest.java`](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/client/logout/AbstractOIDCCClientLogoutTest.java)) and `backchannel_logout_uri`.
- **Authority.** The BFF gets `Auth__Authority` from the AppHost (`WithBffAuth` in `FullstackTemplate.AppHost/AuthProviders.cs`). A conformance provider in `AuthProviders.cs` gives `Authority = https://localhost.emobix.co.uk:8443/test/a/<alias>/`, the suite client ID, and the suite client secret.
- **TLS trust.** The BFF back-channel `HttpClient` must trust the `nginx` certificate. Export it with `docker compose exec nginx cat /etc/ssl/certs/nginx-selfsigned.crt` ([Build and Run wiki](https://gitlab.com/openid/conformance-suite/-/wikis/Developers/Build-&-Run)). Then trust it in the OS store, or in the `BackchannelHttpHandler` of the handler for the conformance configuration only.

## CI automation

- The suite has a REST API: `POST api/plan` creates a plan, `POST api/runner` creates a test module, `GET api/runner/{id}/wait-state` waits for a state, `GET api/info/{id}` and `GET api/log/{id}` give results, and `api/plan/{kind}/{id}` exports HTML or JSON. `api/plan/{id}/certificationpackage` builds the certification package. A hosted instance takes a bearer token (`CONFORMANCE_TOKEN`). The `dev` profile needs no token ([`conformance.py`](https://gitlab.com/openid/conformance-suite/-/blob/master/scripts/conformance.py), [`run-test-plan.py`](https://gitlab.com/openid/conformance-suite/-/blob/master/scripts/run-test-plan.py)).
- `scripts/run-test-plan.py` runs plans, compares the results with files of expected failures and skips, and exits non-zero on an unexpected result. For RP plans it starts a hard-coded sample client (`npm run client` in `sample-openid-client-nodejs`) when a module is `WAITING` ([`run-test-plan.py`](https://gitlab.com/openid/conformance-suite/-/blob/master/scripts/run-test-plan.py)). The suite CI uses this to test RP plans against that sample client ([`.gitlab-ci/run-tests.sh`](https://gitlab.com/openid/conformance-suite/-/blob/master/.gitlab-ci/run-tests.sh)). It cannot drive our BFF without a change.
- So our CI needs its own driver. For each module it: creates the module through the API, waits for `WAITING`, opens a new browser context (Playwright), goes to `/bff/login`, follows the redirects, then for logout tests goes to the `bff:logout_url` from `/bff/user`. Then it waits for `FINISHED` and reads the result. The driver handles the gaps above (fresh BFF key cache for each module). The driver also saves screenshots and BFF logs, because certification needs RP evidence for each test.
- Two CI targets are possible:
  - **Local container in the CI job.** GitHub Actions Linux runners have Docker. The job starts the suite with `docker compose`, starts the Aspire stack with `--isolated`, and runs the driver. Back-channel logout needs `extra_hosts: host.docker.internal:host-gateway` on the `server` container on Linux. Nothing leaves the runner.
  - **Hosted `www.certification.openid.net`.** The BFF calls the suite over the internet, and the browser follows redirects, so a `localhost` BFF works for the Basic, Config, and RP-Initiated plans. Back-channel logout needs a public URL for the BFF, for example a tunnel. The job needs a `CONFORMANCE_TOKEN` secret. The result is on the hosted instance, which is the instance named in the certification form.

## Certification compared with a report

- **A report.** Every run gives results for each module (`PASSED`, `FAILED`, `WARNING`, `REVIEW`, `SKIPPED`) and an HTML or JSON export. It costs nothing. The suite is MIT-licensed ([LICENSE.txt](https://gitlab.com/openid/conformance-suite/-/blob/master/LICENSE.txt)).
- **Pass criteria for certification.** Every module in the profile is `PASSED`, `REVIEW`, `WARNING`, or `SKIPPED`. One `FAILED` or `INTERRUPTED` module blocks the profile. Some tests need a screenshot as evidence ([OIDF RP testing instructions](https://openid.net/certification/connect_rp_testing/)).
- **Submission.** One `.zip` package for each profile, made with "Publish for certification". It holds the signed Certification of Conformance PDF, the suite logs, and a `client-data` folder with RP evidence (logs or screenshots) for each test. An authorized person of the implementer signs the form, not a third party. The form names the suite as `www.certification.openid.net` with its version. OIDF publishes the full logs, so revoke the client credentials before you submit ([RP submission](https://openid.net/certification/connect_rp_submission/)).
- **Fee.** OpenID Connect certification is $700 for members and $3,500 for non-members per new deployment. One payment covers all profiles for that deployment in one calendar year. OP and RP deployments pay separately ([fees](https://openid.net/certification/fees/)).
- **Fee waiver.** The Open-Source Project Certification Policy waives the fee when the requester is responsible for the deployment and no primary maintainer gets pay from an employer for work on the project. OIDF decides each request separately. Send it to `certification@oidf.org` ([open-source policy](https://openid.net/certification/open-source-project-certification-policy/)).

## Open questions

- Do we want a certification (published logs, a signed form, a fee or waiver, the hosted instance), or only a CI report from a local container? This is a decision for the map.
- How does the driver give the BFF a fresh key cache for each module: restart the BFF resource, or a short `RefreshInterval` in a conformance-only configuration? This belongs with the test plan ticket.
