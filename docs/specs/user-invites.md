# Spec: user management and invites

## 1. Scope

A tenant admin adds a colleague to the tenant with an invite link. The admin creates the invite in the SPA, copies the link, and sends it in any way. The colleague opens the link, signs in with the auth provider, and joins the tenant with the role of the invite. This flow works with each auth provider, because the app does not create the account in the provider. The colleague creates or uses their own account, and the claim binds its `sub` to a new user record.

The SPA also gets a users page. It lists the users of the tenant, changes the role of a user, and removes a user. These features use the user endpoints that exist.

This spec leaves out:

- Email delivery of an invite. A later slice can add it as an option that the configuration turns on, with Mailpit in the AppHost.
- The creation of an account in the auth provider (the `TODO handle IDP creation` in `AddUser`). Build it only when a real need appears.
- A change to 0factor.

## 2. Entities and states

| Entity | Root of | Fields that a user sets | States |
|---|---|---|---|
| `Invite` | None | role, email (optional), require email match | Pending, Claimed, Revoked |

An invite also has a tenant, a token hash, an expiry, and, after a claim, the ID of the user that claimed it. A pending invite after its expiry is expired. Expired is not a stored state. The entity computes it from the expiry and the current time. The response DTO gives the answer as `IsExpired`, and the SPA reads that flag.

| From | To | Method | Who |
|---|---|---|---|
| Pending | Claimed | `Invite.Claim` | A signed-in user of a first-party client with no live user record |
| Pending | Revoked | `Invite.Revoke` | A user with `manage_users` in the tenant of the invite |

## 3. Rules

| ID | Rule | Enforced by | Backstop |
|---|---|---|---|
| UM-1 | The users page lists the users of the tenant of the caller, with a pager. | `GetUserList` and its tenant filter | |
| UM-2 | The users page changes the role of a user. The page shows the control only to a caller who can change the role. | `UpdateUserRole` handler check | |
| UM-3 | The users page removes a user after the caller confirms. The page shows the control only to a caller who can remove the user. | `DeleteUser` handler check | |
| INV-1 | Only a caller with `manage_users` and each default permission of the role can create an invite. A caller cannot invite with a role that has more permissions than the caller has. | `AddInvite` handler check | |
| INV-2 | The server stores only a hash of the token. The create response gives the link one time only. A list or a get of an invite never gives the token. | `Invite.Create` | |
| INV-3 | The link puts the token in the URL fragment (`/join#token=...`), so the token is not in a server log, a BFF log, or a `Referer` header. The SPA moves a token in the fragment of any path to the tab, and goes to the join page. In a tab, the token is for the first `sub` that reads it. The tab forgets the token when the join page sees another `sub`, and after a 404 or a 410 of the preview or the claim. A token that waits in a tab gives no form on another page. Before a claim, the SPA reads the session again, and it sends no claim when the `sub` is not the `sub` that the form was filled for. A claim that the SPA reads as an earlier win of the same user is a success, so the tab forgets the token as after each success. | SPA `invite-link.ts` and `join` route | |
| INV-4 | An invite expires 7 days after its creation. A claim of an expired invite fails. | `Invite.Claim` | |
| INV-5 | An invite has one use. A claim of an invite that is claimed or revoked fails. | `Invite.Claim` | Version check of `Invite` |
| INV-6 | Only a signed-in user of a first-party client with no live user record can claim. If the `sub` has a live user in any tenant, the claim fails, because a user belongs to one tenant only. | `ClaimInvite` handler check, `ConflictException` (409) | Unique index on the live `users.identifier` |
| INV-7 | A claim adds a user in the tenant of the invite, with the role of the invite and the `sub` of the caller. The claim and the user add are in one save, and the save takes the shared lock of the tenant. | `ClaimInvite` handler | Shared tenant lock |
| INV-8 | The join page fills the first name, the last name, the email, and the username from the token claims of the caller. The user can change each value before the claim. The exception is the email on an invite with an email match: the field is read-only, because the new user gets the email of the token (INV-9). | SPA `join` route, `UserForCreationDto` validation | |
| INV-9 | When an invite requires an email match, the claim fails unless the access token of the caller has the email of the invite in `email` and `true` in `email_verified`. The comparison of the emails is case-insensitive. A provider that sends no `email` or no `email_verified` fails the match. Section 8 gives the providers where the match works. After a match, the new user gets the email of the token, not the email of the request body. | `Invite.Claim` | |
| INV-10 | A user with `manage_users` can revoke a pending invite of their tenant. | `Invite.Revoke` | |
| INV-11 | A tenant delete deletes each invite of the tenant. | `TenantOwnedRows` | `TenantOwnedRowsTests` |
| INV-12 | The audit log hides the token hash from each reader without `read_audit_log_sensitive`. The hash cannot claim an invite, because a claim hashes the token that it gets. | Audit sensitivity of `Invite` (see [audit-log.md](../backend/audit-log.md)) | `AuditSensitivityTests` |

## 4. Permissions

None. The invite features use `manage_users`, which exists.

## 5. Endpoints and tools

| Feature | HTTP | gRPC | MCP tool | Permission |
|---|---|---|---|---|
| `AddInvite` | `POST /api/v1/invites` | | | `manage_users` |
| `GetInviteList` | `GET /api/v1/invites` | | | `manage_users` |
| `RevokeInvite` | `POST /api/v1/invites/{id}/revoke` | | | `manage_users` |
| `GetInvitePreview` | `POST /api/v1/invites/preview` | | | A signed-in user |
| `ClaimInvite` | `POST /api/v1/invites/claim` | | | A signed-in user with no user record |

`GetInvitePreview` and `ClaimInvite` take the token in the body, not in the path or the query, so the token is not in a log. The preview gives the tenant name and the role, so the join page can show what the user joins before the claim.

## 6. Messages

| Rule | Status | Message |
|---|---|---|
| INV-2 | 404 | This invite link is not valid. Ask a tenant admin for a new invite. |
| INV-4 | 410 | This invite expired. Ask a tenant admin for a new invite. |
| INV-5 | 410 | This invite was used or revoked. Ask a tenant admin for a new invite. |
| INV-6 | 409 | Your account already belongs to a tenant. |
| INV-9 | 403 | This invite is for another email address. Sign in with the account of that email. |
| INV-9 | 403 | The email address of your account is not verified. Verify it, then open the invite link again. |

The SPA writes one more message. A tab with no token asks the Server nothing and shows "This page needs an invite link. Open the invite link again." A provider can continue the sign-in in a new tab, and that tab has no token, so the link can still be good. A token that the Server refuses keeps the message of the Server.

## 7. Time zones

| Value | Stored as | Shown in the zone of |
|---|---|---|
| Invite expiry | `timestamptz` (UTC) | The browser of the user |

## 8. Open decisions

| Decision | Who decides | Rules that wait |
|---|---|---|
| The default of "require email match" is off. The link is the secret, and it has one use and a 7-day expiry. Providers handle `email_verified` differently, and the Duende Demo users have fixed emails. Change the default to on if a forwarded link is a risk. | The template owner | INV-9 |
| The expiry is 7 days, and it is not a setting. | The template owner | INV-4 |
| A pending invite stays valid after its creator loses `manage_users` or is deleted. The invite does not record its creator. The alternative is to record the creator, and to revoke the pending invites of a demoted or deleted creator in a domain event handler. | The template owner | INV-1, INV-10 |
| The Server lets an admin remove their own user, or change the role of the last admin. Thus a tenant can lose its last admin. A guard for the last admin is a Server rule outside this spec. | The template owner | None |

The email match of INV-9 reads `email` and `email_verified` from the access token. This table gives the result on each local provider of `FullstackTemplate.AppHost/AuthProviders/`. "Measured" means that a run on an isolated stack read an access token of the provider and sent a claim through the BFF. The other rows come from the seed and from documents or source of the vendor, so a run can give another result.

| Provider | `email_verified` in the access token | Self-registration | Result of the email match | Source |
|---|---|---|---|---|
| ZeroFactor | JSON `true` for each user, also for a user that registers on the login page | Yes | Each user with the email of the invite passes. Anyone can register with any email, so the match does not stop a forwarded link on ZeroFactor. | Measured |
| Keycloak | JSON `true` for a seed user. JSON `false` for a self-registered user, because the realm does not verify an email. | Yes | A seed user passes. A self-registered user is refused. | Measured |
| FusionAuth | Not known. The seed does not set the scope policy. The Strict policy leaves out `email` and `email_verified`. The Compatibility policy sends a JSON boolean. | Yes | Not known. With the Strict policy, each user is refused. | Seed and vendor documents |
| Authentik | JSON `false` for each user, from the managed email scope mapping of version 2026.8.5 | Probably no. The seed adds no enrollment flow. | Each user is refused. | Seed and vendor source |
| Cognito | Absent, with `email`. A Cognito access token has no email. | Probably yes, with the SignUp API. Not examined on the local emulator. | Each user is refused. | Provider class and AWS documents |
| Duende Demo | Absent. The API resource of the demo has only `name` and `email`. | Yes, on `/Account/Create` | Each user is refused. | Source of the demo on GitHub |
| Conformance | JSON `false`, from the userinfo response | No | Each user is refused. | Source of the suite on GitHub |
