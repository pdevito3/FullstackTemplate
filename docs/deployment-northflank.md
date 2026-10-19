# Northflank deployment

`northflank.json` is the infrastructure-as-code template for Northflank.

In production, the BFF serves the SPA build from its `wwwroot`. The server is an internal API only.

## Keycloak

With `--AuthProvider Keycloak`, the template deploys these services:

- PostgreSQL for Keycloak
- The Keycloak server (public)
- The `keycloak-config` service, which applies the realm configuration

`keycloak-config-cli` applies the configuration from `infra/keycloak/`. It creates the realm, the client, the roles, and (optionally) the test users.

### After the first deployment

1. Get the Keycloak server URL from the Northflank dashboard.
2. Set the `KEYCLOAK_BASE_URL` argument in Northflank to this URL.
3. Build and run the `keycloak-config` service.
4. When the configuration is applied, scale `keycloak-config` to 0 instances.

The `keycloak-config` service does these steps:

- It waits until Keycloak is healthy.
- It creates the `aspire` realm.
- It configures the `aspire-app` client with the correct redirect URIs.
- It creates the `admin` and `user` roles.
- If `KC_INCLUDE_TEST_USERS=true`, it creates the test users.

| Environment | `KC_INCLUDE_TEST_USERS` | Notes |
|-------------|-------------------------|-------|
| UAT / Dev   | `true`                  | Creates admin@example.com and user@example.com |
| Production  | `false`                 | Creates no test users |

### Change the configuration

1. Edit the files in `infra/keycloak/` (see `infra/keycloak/README.md`).
2. Commit and push.
3. Rebuild and restart the `keycloak-config` service in Northflank.
4. Scale the service back to 0 instances.

## Authentik

With `--AuthProvider Authentik`, the template deploys these services:

- PostgreSQL for Authentik
- Redis for Authentik
- The Authentik server (public)
- The Authentik worker

### After the first deployment

1. Get the Authentik server URL from the Northflank dashboard.
2. Set the `AUTHENTIK_BASE_URL` argument in Northflank to this URL.
3. Open the Authentik admin at `{AUTHENTIK_BASE_URL}/if/admin/`.
4. Log in as `akadmin` with the value of `AUTHENTIK_BOOTSTRAP_PASSWORD`.
5. Go to Applications > Providers > Create, and create an OAuth2/OpenID provider with these values:
   - Name: FullstackTemplate Provider
   - Authorization flow: default-provider-authorization-implicit-consent
   - Client ID: `aspire-app`
   - Client Secret: the value of the `AUTH_CLIENT_SECRET` argument
   - Redirect URIs: `https://{your-bff-url}/signin-oidc`
   - Scopes: openid, email, profile, offline_access
6. Go to Applications > Applications > Create, and create an application with these values:
   - Name: FullstackTemplate
   - Slug: `aspire-app`
   - Provider: the provider from step 5
7. Create the `admin` and `user` groups, and the users that you need.
