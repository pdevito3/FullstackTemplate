# Invites

A tenant admin invites a colleague from `/users` and gets a link to copy. The colleague opens the link, signs in, sees what they join, and joins the tenant. The token of the link is in the URL fragment, and it never goes in a URL that the browser sends.

## Sub-features

- `button` shows `Invite a user` on `/users` only when `allowedActions` of the tenant has `InviteUser`. A plain member has no button and sends no `GET /api/v1/invites`.
- `dialog` asks for the role, an optional email, and `Require email match`. It shows the refusal of the Server on the email field when the match has no email.
- `link` shows the link `<origin>/join#token=<token>` in the dialog `Send this link to your colleague`, with `Copy link` and the sentence that this is the only time that the link shows. Escape does not close this dialog.
- `list` shows the section `Invites` with the email, the role, the state, and the expiry in the zone of the browser. The state `Expired` comes from `isExpired` of the Server, not from the clock of the browser. It has a pager with the search key `invitesPage`.
- `revoke` asks for a confirmation in the dialog `Revoke the invite for <email>?`, sends `POST /api/v1/invites/{id}/revoke`, shows the toast `Invite revoked` one time, and moves the focus to the heading `Invites`.
- `token` moves the token from the fragment to the `sessionStorage` of the tab before the session check reads the URL, on each path. The return URL of the login is `/join`. The first `sub` that reads the token holds it, and the tab forgets it when another `sub` appears.
- `preview` shows the tenant, the role, and the expiry on `/join`. A dead link shows the message of the Server and no form, and the tab forgets the token. A tab with no token shows `This page needs an invite link. Open the invite link again.` A member sees `Your account already belongs to a tenant.` and no form.
- `claim` fills the form `Join the tenant` from the claims of the account, sends `POST /api/v1/invites/claim`, shows one toast that names the tenant, and goes to the home page. On an invite with an email match, the email is read-only.

## How to get to it (user POV)

- Choose `Users` in the sidebar, then `Invite a user`.
- Open a link `<origin>/join#token=<token>`, with or without a session.

## Driving it with browse.mjs

Preconditions:

- `doctor.sh` exits with 0.
- The stack uses ZeroFactor, because the recipe needs two users. `admin@example.com` and `user@example.com` have no user record. This is true on a new stack.

- **Start.** `login('admin@example.com')`, then start a tenant at `/start-tenant`. The response is 201.
- **Refusal.** Choose `Invite a user`, click `Require email match`, and choose `Create link` after `waitForResponse` on `POST /api/v1/invites`. The response is 422. The alert in the dialog has the message of the Server, and the field `Email (optional)` has the focus.
- **Link.** Fill the form again, and choose `Create link`. The response is 201. The value of `Invite link` is `<origin>/join#token=<token of the response>`. Choose `Copy link`, and read the same value from the clipboard.
- **Revoke.** Reload `/users`. Choose the `Revoke` button of the row, then `Revoke invite` after `waitForResponse` on `POST /api/v1/invites/{id}/revoke`. The response is 200, the toast has count 1, and the row shows `Revoked` with no button.
- **Another person in the tab.** `page.goto(<good link>)` as the admin, who is a member, so the tab keeps the token for the admin. `context.clearCookies()` ends the session with no logout in the tab. `page.goto('/join')` and `login('user@example.com')`. The alert has the message of a tab with no token, the page has no form, and `sessionStorage` has no `invite-token`.
- **Dead link.** `logout()`, then `page.goto(<link of the revoked invite>)`. The request to `/bff/login` has `returnUrl` `/join`. `login('user@example.com')`. The preview answers 410. The alert has the message of the Server, and the page has no form.
- **Good link.** `logout()` from the home page, then `page.goto(<good link>)` and `login('user@example.com')`. The preview answers 200, also after `page.reload()`, and its request body has the token. The URL ends in `/join`.
- **Second link in the tab.** `page.goto(<link with an email match>)`. The page shows that invite. The field `Email` is read-only.
- **Claim.** `page.goto(<good link>)`, fill the form, and choose `Join tenant` after `waitForResponse` on `POST /api/v1/invites/claim`. The response is 201. The page is `/`, the toast `You joined <tenant>.` has count 1, and `sessionStorage` has no `invite-token`.
- **Member.** `page.goto(<a link>)`. The page shows `Your account already belongs to a tenant.` and no form.
- **Plain member.** Open `/users`. `GET /api/v1/tenants/{id}` has an empty `allowedActions`. The page has no button and no section `Invites`, and the page sends no `GET /api/v1/invites`.
- **Claimed.** `logout()`, `login('admin@example.com')`, and open `/users`. The rows show `Claimed` and `Revoked`.
- **Proof.** Run `node $S/scripts/browse.mjs $S/recipes/invites.mjs`. The captures `users-admin-empty`, `invite-dialog-refused`, `invite-dialog-form`, `invite-dialog-link`, `invites-pending`, `revoke-dialog`, `invite-revoked`, `join-no-link`, `join-revoked`, `join-preview`, `join-email-match`, `join-form-filled`, `joined-home`, `join-member`, `users-plain-member`, and `invites-claimed` show each state.

## Gotchas

- The recipe needs two users, so it runs on ZeroFactor only. `login()` of the DuendeDemo has other users.
- The Server caches the tenant of each user. The recipe deletes its tenant as the admin through `DELETE /api/v1/tenants/{id}` at the end. If a run stops before that step, run `dotnet aspire resource server restart --apphost FullstackTemplate.AppHost` before the next run.
- A logout forgets the token of the tab. After a logout, open the link again.
- A `page.goto` to a link in a tab that shows `/join` changes only the fragment, and the page reloads. Wait for the preview, not for a navigation.
- The screenshots of the dialog show a live link until the recipe deletes the tenant. Do not copy a link from a run into a real message.
