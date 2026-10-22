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

The script `frontend/e2e/run-against-stack.ts` reads the `webfrontend` and `server` URLs from `dotnet aspire describe`. It finds the IdP whose `resource` is in the stack, or the IdP with no `resource` if the stack has none. Then it sets `BASE_URL`, `SERVER_URL`, `AUTHORITY`, and `IDP`, and runs `playwright test`. To override the IdP, set `IDP` to the name of a file in `frontend/e2e/idps/`. Extra arguments go to Playwright, for example `pnpm test:e2e:stack --repeat-each=5`.

The script also gives the tests the client id and the client secret of the BFF, and the admin credentials of the provider. It never prints them.

- The refresh rotation test uses the client of the BFF for its own code flow. It proves that the provider rejects the old refresh token after one refresh. It runs if `sessionFeatures.refreshRotation` is true in the IdP file: Keycloak, FusionAuth, and Authentik.
- The back-channel logout test ends the session at the provider through its admin API, and expects a 401 from `/bff/user`. It runs if `sessionFeatures.backChannelLogoutReachesBff` is true in the IdP file: on Keycloak, with the bootstrap admin, and on Authentik, with the `authentik-bootstrap-token` parameter of the AppHost. Authentik creates this token only on the first start of its database.

## Test users

The seed files of each provider create its test users. The doc comment of each provider class in `FullstackTemplate.AppHost/AuthProviders/` lists them. The file in `frontend/e2e/idps/` gives the user that the e2e tests log in with.
