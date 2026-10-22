# Provider features by discovery

> Superseded in part by [ADR 0007](0007-regular-repo-instead-of-template.md): `AuthProviders.cs` is now one folder for each provider in `AuthProviders/`, and the repo is no longer a template.

The BFF reads the discovery document of the identity provider to decide which optional features it uses. Each feature has a setting with three values: `Auto` uses the feature if discovery lists it, `Require` stops the BFF at startup if discovery does not list it, and `Disable` never uses it. The features are revocation, the RFC 9207 `iss` parameter, back-channel logout, the `end_session_endpoint`, PAR, and (in phase 2) DPoP. A missing feature under `Auto` gets a safe fallback and a startup warning. As a result, "compliant" is a property of the BFF: the BFF obeys each rule whenever the identity provider offers the feature, and it never uses an unsafe fallback. The design is in [the home-rolled BFF spec](../specs/home-rolled-bff.md#33-provider-features-and-settings).

## Considered options

- **A flag for each provider in `AuthProviders.cs`**, as `RevokeRefreshTokenOnLogout` is today. We did not choose this option. A flag can be wrong after a provider upgrade, and an app that comes from the template uses a provider that the AppHost does not know.
- **Refuse to start without the full compliance set.** We did not choose this option. FusionAuth has no revocation and no back-channel logout, so the template could not run with it. `Require` gives this behavior to a team that wants it.

## Consequences

- With `Auto`, the active features follow the identity provider. An upgrade of the provider can add or remove a feature with no change of code. The startup warnings and the live discovery test of the e2e tests make each change visible.
- The `iss` check obeys RFC 9207 section 2.4. A callback with no `iss` passes only if discovery does not set `authorization_response_iss_parameter_supported` to `true`. An `iss` that is present must always match.
- Logout always deletes the BFF session first. Revocation and RP-initiated logout are steps after it that the BFF can skip.
- The e2e tests skip a feature test for a provider that lacks the feature, and they prove the fallback in its place.
