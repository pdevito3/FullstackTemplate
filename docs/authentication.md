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

## Auth provider

The AppHost holds all provider config. The BFF and the server have no provider code. They read the `Auth:*` settings that the AppHost gives them, and the BFF reads the discovery document of the provider for the optional features ([ADR 0005](adr/0005-provider-features-by-discovery.md)). See [ADR 0007](adr/0007-regular-repo-instead-of-template.md).

### Select a provider

The `AuthProvider` setting in `FullstackTemplate.AppHost/appsettings.json` selects the provider at runtime. Its value is the name of a folder in `FullstackTemplate.AppHost/AuthProviders/`, without case sensitivity. The setting has no fallback: if it is empty or not known, the AppHost stops and lists the known providers.

- To change the default, run `dotnet run scripts/init.cs --auth-provider <Name>`, or edit the setting.
- To use another provider for one run, set the environment variable. For example:

  ```bash
  AuthProvider=Keycloak dotnet aspire start --isolated --non-interactive --apphost FullstackTemplate.AppHost
  ```

### Add a provider

1. Add the folder `FullstackTemplate.AppHost/AuthProviders/<Name>/`.
2. In the folder, add the class `<Name>AuthProvider` that implements `IAuthProvider`. The AppHost finds the class by reflection, and `<Name>` from the class name is the value of the `AuthProvider` setting. `AddTo` adds the containers of the provider to the stack and returns an `AuthProviderConfig`.
3. Put the seed files in `seed/`, and mount them with `WithSeedFiles`. Call `WithClientUrls` on the container, and read the app URLs from the `APP_*` variables in the seed files. An isolated run gives the apps random ports.
4. Add `frontend/e2e/idps/<name>.json`, with `<name>` in lowercase. Give the login form and the test user. Set `resource` to the name of the Aspire resource of the provider.

A unit test in `AuthProviderCatalogTests` makes sure that each provider has its folder and its e2e file.

### Delete a provider

1. Delete the folder `FullstackTemplate.AppHost/AuthProviders/<Name>/`.
2. Delete `frontend/e2e/idps/<name>.json`.
3. If the `AuthProvider` setting names the deleted provider, select another provider.

`AuthProviderConfig` holds each setting that can differ between providers, with the reason in its doc comments. The doc comments of each provider class give the differences of that provider and its test users.

## E2E tests

The Playwright tests in `frontend/e2e/auth-flows.spec.ts` log in through the React app and through Swagger UI. They run against the provider of the running stack.

1. Start the stack, as shown in [Select a provider](#select-a-provider).
2. Wait until the server, the BFF, and the webfrontend are healthy:

   ```bash
   dotnet aspire wait server --status healthy --apphost FullstackTemplate.AppHost
   dotnet aspire wait bff --status healthy --apphost FullstackTemplate.AppHost
   dotnet aspire wait webfrontend --status healthy --apphost FullstackTemplate.AppHost
   ```

3. In `frontend`, run the tests:

   ```bash
   pnpm test:e2e:stack
   ```

The script `frontend/e2e/run-against-stack.ts` reads the `webfrontend` and `server` URLs from `dotnet aspire describe`. It finds the IdP whose `resource` is in the stack, or the IdP with no `resource` if the stack has none. Then it sets `BASE_URL`, `SERVER_URL`, and `IDP`, and runs `playwright test`. To override the IdP, set `IDP` to the name of a file in `frontend/e2e/idps/`. Extra arguments go to Playwright, for example `pnpm test:e2e:stack --repeat-each=5`.

## Test users

The seed files of each provider create its test users. The doc comment of each provider class in `FullstackTemplate.AppHost/AuthProviders/` lists them. The file in `frontend/e2e/idps/` gives the user that the e2e tests log in with.
