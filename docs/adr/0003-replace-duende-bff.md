# Replace Duende BFF with our own BFF

The BFF no longer uses `Duende.BFF` or `Duende.BFF.Yarp`. Our own code in `FullstackTemplate.Bff` does the BFF work, on top of the Microsoft `OpenIdConnect` and `Cookies` handlers, YARP, and `Duende.AccessTokenManagement.OpenIdConnect` (Apache 2.0). As a result, the template has no dependency with a license cost, and each BFF behavior is visible in our code. The wire contract for the browser does not change. The design is in [the home-rolled BFF spec](../specs/home-rolled-bff.md).

## Considered options

- **Keep Duende BFF.** It is free below a revenue limit, and it does the work today. We did not choose this option. Each app that comes from the template inherits the license terms, and most of the BFF behavior is hidden in a startup filter and in decorated services. Its back-channel logout ends no session without its paid server-side session feature.
- **Write our own OIDC client too.** We did not choose this option. The Microsoft handler already does the code flow, PKCE, `state`, `nonce`, the ID token checks, PAR, and RP-initiated logout correctly. We add only the checks that it does not have.
- **Write our own token management.** We did not choose this option. `Duende.AccessTokenManagement` has an open license, and it already does the refresh, the revocation, the client authentication, and the client side of DPoP.
- **A separate BFF library.** We did not choose this option. The template has one BFF, so a folder for each concern in `FullstackTemplate.Bff` is enough.

## Consequences

- We own the security code of the BFF: the `X-CSRF` check, the 302 rewrite, the local URL rule, the `sid` check, the RFC 9207 `iss` check, the back-channel logout receiver, and the BFF session store. Each rule has its own tests, and CI runs the OpenID Foundation conformance suite on each BFF change.
- The cutover is one change with no side-by-side flag. The first slice of the build must reach parity before it merges.
- A security fix in Duende BFF does not come to us. We must watch the OAuth and OIDC specs and the advisories for the Microsoft handlers, YARP, and `Duende.AccessTokenManagement`.
- The `DuendeDemo` auth provider stays. It is a public IdP, not a package.
