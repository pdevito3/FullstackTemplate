# Authentication

The app uses the Backend-For-Frontend (BFF) pattern. The browser never holds a token. `FullstackTemplate.Bff` (Duende BFF and YARP) holds the tokens and sends API calls to the server with a JWT.

## Login flow

1. The user clicks Login in the React app.
2. The frontend goes to `/bff/login`.
3. The BFF sends the browser to the OIDC provider.
4. The provider sends the browser back to `/signin-oidc`.
5. The BFF creates the secure `__Host-bff` cookie.
6. The React app calls `/bff/user` to get the claims.
7. API calls go through the BFF at `/api/v1/*`.

## Providers

The options are Keycloak, FusionAuth, Authentik, Cognito, and Duende Demo. Keycloak, FusionAuth, Authentik, and Cognito run as containers in the stack. Their seed files read the app URLs from environment variables, so the redirect URIs work with the random ports of an isolated run.

In a project from the template, the `--AuthProvider` option of `dotnet new` selects the provider. The default is `DuendeDemo`.

In this repository, the `AuthProvider` setting of the AppHost selects the provider: `DuendeDemo` (the default), `Keycloak`, `FusionAuth`, `Authentik`, or `Cognito`. For example, this command starts an isolated stack with Keycloak:

```bash
AuthProvider=Keycloak dotnet aspire start --isolated --non-interactive --apphost FullstackTemplate.AppHost
```

### Cognito

[Floci](https://github.com/floci-io/floci) emulates Amazon Cognito in the `cognito` container. The init hook `FullstackTemplate.AppHost/cognito/ready.d/01-seed.sh` creates the user pool, the app client, the groups, and the test users at each start. Floci keeps no data, so each start gets a new pool. The hook pins the pool ID (`us-east-1_aspire`), the client ID, and the client secret, so the AppHost knows the authority before Floci starts.

Cognito is different from the other providers in four ways. The AppHost sets each difference in `AuthProviders.Cognito`:

- Cognito rejects the `offline_access` scope. It returns a refresh token without it. Thus `Auth:RequestOfflineAccess` is `false`.
- The access token has no `aud` claim. It has `client_id` instead. Thus `Auth:AudienceClaimType` is `client_id`, and the server compares that claim with `Auth:Audience`.
- The discovery document has no `end_session_endpoint` and no `revocation_endpoint`. The BFF reads the logout URL from `Auth:LogoutEndpoint` and sends `client_id` and `logout_uri` to it. The BFF does not revoke the refresh token.
- The roles are in `cognito:groups`. The access token has no `email`, and its `username` is the `sub` UUID.

The issuer comes from `FLOCI_BASE_URL`. The AppHost sets it to the host URL of the container, so the issuer is the URL that the BFF, the server, and the browser use.

## E2E tests

The Playwright tests in `frontend/e2e/auth-flows.spec.ts` log in through the React app and through Swagger UI. They use Keycloak in the stack, not the public Duende demo.

1. Start the stack with Keycloak, as shown above. In a project from the template, create the project with `--AuthProvider Keycloak` and start the stack without the setting.
2. Wait until the server, the BFF, and the webfrontend are healthy:

   ```bash
   dotnet aspire wait server --status healthy --apphost FullstackTemplate.AppHost
   dotnet aspire wait bff --status healthy --apphost FullstackTemplate.AppHost
   dotnet aspire wait webfrontend --status healthy --apphost FullstackTemplate.AppHost
   ```

3. In `frontend`, run the tests:

   ```bash
   pnpm test:e2e:keycloak
   ```

The script `frontend/e2e/run-against-stack.ts` reads the `webfrontend` and `server` URLs from `dotnet aspire describe`. Then it sets `BASE_URL`, `SERVER_URL`, and `IDP` (`keycloak`, unless `IDP` is set), and runs `playwright test`. For Cognito, start the stack with `AuthProvider=Cognito` and run `pnpm test:e2e:cognito`. Extra arguments go to Playwright, for example `pnpm test:e2e:keycloak --repeat-each=5`.

## Test users

### Keycloak, FusionAuth, Authentik, and Cognito

| Email | Password | Roles |
|-------|----------|-------|
| admin@example.com | password123! | admin, user |
| user@example.com | password123! | user |

### Authentik admin

| Email | Password |
|-------|----------|
| akadmin@example.com | password123! |

### Duende Demo

| Username | Password |
|----------|----------|
| bob | bob |
| alice | alice |
