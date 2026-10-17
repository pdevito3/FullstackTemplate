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

`FullstackTemplate.AppHost/AppHost.cs` selects the provider. The options are Keycloak, FusionAuth, Authentik, and Duende Demo.

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
