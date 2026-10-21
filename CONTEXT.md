# FullstackTemplate

A multi-tenant web app template. A browser talks only to the BFF, and the BFF signs the user in with an external identity provider and calls the server API for the user.

## Language

### Authentication

**BFF session**:
The server-side record of one signed-in browser at the BFF. It holds the user's tokens, and the browser holds only a cookie that points to it.
_Avoid_: Cookie session, user session, login

**IdP session**:
The user's signed-in state at the identity provider. One IdP session can back many BFF sessions, and it can end without the browser present.
_Avoid_: SSO session, provider session

**Identity provider**:
The external OpenID Connect server that authenticates the user (Keycloak, FusionAuth, Authentik, Cognito, or the Duende Demo).
The `AuthProvider` setting selects it.
_Avoid_: OP, IdP server
