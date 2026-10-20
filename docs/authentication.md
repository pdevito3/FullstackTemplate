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

The options are Keycloak, FusionAuth, Authentik, and Duende Demo. Keycloak, FusionAuth, and Authentik run as containers in the stack. Their seed files read the app URLs from environment variables, so the redirect URIs work with the random ports of an isolated run.

In a project from the template, the `--AuthProvider` option of `dotnet new` selects the provider. The default is `DuendeDemo`.

In this repository, the `AuthProvider` setting of the AppHost selects the provider: `DuendeDemo` (the default), `Keycloak`, `FusionAuth`, or `Authentik`. For example, this command starts an isolated stack with Keycloak:

```bash
AuthProvider=Keycloak dotnet aspire start --isolated --non-interactive --apphost FullstackTemplate.AppHost
```

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

The script `frontend/e2e/run-against-stack.ts` reads the `webfrontend` and `server` URLs from `dotnet aspire describe`. Then it sets `BASE_URL`, `SERVER_URL`, and `IDP=keycloak`, and runs `playwright test`. Extra arguments go to Playwright, for example `pnpm test:e2e:keycloak --repeat-each=5`.

## Test users

### Keycloak, FusionAuth, and Authentik

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
