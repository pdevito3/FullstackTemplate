export default async ({ page, context, stack, step, capture, settle, expect, login, logout }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write'], { origin: new URL(stack.urls.web).origin })

  const stamp = Date.now().toString(36)
  const answered = (method, pattern) =>
    page.waitForResponse((r) => r.request().method() === method && pattern.test(new URL(r.url()).pathname))
  const invitesTable = () => page.getByRole('grid', { name: 'Invites' }).or(page.getByRole('table', { name: 'Invites' }))
  const inviteRow = (invite, invites) =>
    invitesTable().getByRole('row').filter({ hasText: invites.find((i) => i.id === invite.id).email })

  const ADMIN = 'admin@example.com'
  let signedIn = null
  const signIn = async (email) => {
    await login(email)
    signedIn = email
  }
  const signOut = async () => {
    await page.goto('/')
    await logout()
    signedIn = null
  }

  // A token must not reach a URL. The browser sends a fragment to no server, and Playwright reports a request without it.
  const requestUrls = []
  page.on('request', (r) => requestUrls.push(decodeURIComponent(r.url())))
  const expectNoTokenInAUrl = (...tokens) => {
    for (const token of tokens) {
      expect(requestUrls.filter((url) => url.includes(token))).toEqual([])
      expect(decodeURIComponent(page.url())).not.toContain(token)
    }
  }

  const createInvite = async ({ role, email, match }, label) => {
    await page.getByRole('button', { name: 'Invite a user', exact: true }).click()
    const dialog = page.getByRole('dialog', { name: 'Invite a user' })
    await expect(dialog).toBeVisible()
    await settle()
    if (role !== 'User') {
      await dialog.getByRole('button', { name: / Role$/ }).click()
      await settle()
      await page.getByRole('option', { name: role, exact: true }).click()
    }
    await dialog.getByRole('textbox', { name: 'Email (optional)' }).fill(email)
    if (match) await dialog.getByText('Require email match', { exact: true }).click()
    if (label) await capture(`${label}-form`, { fullPage: false })
    const created = answered('POST', /^\/api\/v1\/invites$/)
    await dialog.getByRole('button', { name: 'Create link', exact: true }).click()
    const res = await created
    expect(res.status()).toBe(201)
    const body = await res.json()
    const linkDialog = page.getByRole('dialog', { name: 'Send this link to your colleague' })
    const field = linkDialog.getByRole('textbox', { name: 'Invite link' })
    await expect(field).toHaveValue(`${new URL(stack.urls.web).origin}/join#token=${body.token}`)
    await settle()
    if (label) await capture(`${label}-link`, { fullPage: false })
    await linkDialog.getByRole('button', { name: 'Copy link', exact: true }).click()
    await expect(linkDialog.getByText('Link copied.', { exact: true })).toBeVisible()
    const link = await field.inputValue()
    expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(link)
    await linkDialog.getByRole('button', { name: 'Done', exact: true }).click()
    await expect(linkDialog).toBeHidden()
    await expect(page.getByRole('button', { name: 'Invite a user', exact: true })).toBeFocused()
    return { id: body.invite.id, token: body.token, link }
  }

  const fillPerson = async (form, { firstName, lastName, email, username }) => {
    await form.getByRole('textbox', { name: 'First name' }).fill(firstName)
    await form.getByRole('textbox', { name: 'Last name' }).fill(lastName)
    await form.getByRole('textbox', { name: 'Email' }).fill(email)
    await form.getByRole('textbox', { name: 'Username' }).fill(username)
  }

  const tenant = await step('admin@example.com starts a tenant', async () => {
    await signIn(ADMIN)
    await page.goto('/start-tenant')
    const form = page.getByRole('form', { name: 'Start a tenant' })
    await form.getByLabel('Tenant name', { exact: true }).fill(`Verify ${stamp}`)
    await fillPerson(form, { firstName: 'Admin', lastName: 'User', email: ADMIN, username: `admin-${stamp}` })
    const started = answered('POST', /^\/api\/v1\/users\/initiate$/)
    await form.getByRole('button', { name: 'Start a tenant' }).click()
    const res = await started
    expect(res.status()).toBe(201)
    return (await res.json()).tenant
  })

  try {
    await step('the admin sees the button and an empty list of invites', async () => {
      const list = answered('GET', /^\/api\/v1\/invites$/)
      await page.goto('/users')
      expect((await list).status()).toBe(200)
      await expect(page.getByRole('button', { name: 'Invite a user', exact: true })).toBeVisible()
      await expect(page.getByText('There are no invites. Choose Invite a user to make a link.', { exact: true })).toBeVisible()
    })
    await capture('users-admin-empty')

    await step('an email match with no email shows the refusal of the Server on the field', async () => {
      await page.getByRole('button', { name: 'Invite a user', exact: true }).click()
      const dialog = page.getByRole('dialog', { name: 'Invite a user' })
      await expect(dialog).toBeVisible()
      await settle()
      await dialog.getByText('Require email match', { exact: true }).click()
      const refused = answered('POST', /^\/api\/v1\/invites$/)
      await dialog.getByRole('button', { name: 'Create link', exact: true }).click()
      expect((await refused).status()).toBe(422)
      await expect(dialog.getByRole('alert')).toContainText('Please give the email that the invite is for')
      await expect(dialog.getByRole('textbox', { name: 'Email (optional)' })).toBeFocused()
      await settle()
    })
    await capture('invite-dialog-refused', { fullPage: false })
    await step('cancel the refused form', async () => {
      const dialog = page.getByRole('dialog', { name: 'Invite a user' })
      await dialog.getByRole('button', { name: 'Cancel', exact: true }).click()
      await expect(dialog).toBeHidden()
    })

    const claimed = await step('invite user@example.com through the dialog, and read the link', () =>
      createInvite({ role: 'User', email: 'user@example.com', match: false }, 'invite-dialog'),
    )
    const toRevoke = await step('invite a second person', () =>
      createInvite({ role: 'Admin', email: `revoke-${stamp}@example.com`, match: false }),
    )
    const matching = await step('invite with an email match', () =>
      createInvite({ role: 'User', email: `match-${stamp}@example.com`, match: true }),
    )
    const tokens = [claimed.token, toRevoke.token, matching.token]

    const listed = await step('the list shows the invites and no token', async () => {
      const list = answered('GET', /^\/api\/v1\/invites$/)
      await page.reload()
      const res = await list
      const text = await res.text()
      for (const token of tokens) expect(text).not.toContain(token)
      const invites = JSON.parse(text)
      await expect(inviteRow(claimed, invites)).toContainText('Pending')
      await expect(inviteRow(toRevoke, invites)).toContainText('Pending')
      await expect(inviteRow(matching, invites)).toContainText('Email must match')
      return invites
    })
    await capture('invites-pending')

    await step('Revoke asks for a confirmation', async () => {
      await inviteRow(toRevoke, listed).getByRole('button', { name: /^Revoke / }).click()
      await expect(page.getByRole('dialog', { name: /^Revoke / })).toBeVisible()
      await settle()
    })
    await capture('revoke-dialog', { fullPage: false })

    await step('Revoke invite: the Server revokes it, and the list shows it', async () => {
      const revoked = answered('POST', new RegExp(`^/api/v1/invites/${toRevoke.id}/revoke$`))
      await page.getByRole('dialog').getByRole('button', { name: 'Revoke invite', exact: true }).click()
      const res = await revoked
      expect(res.status()).toBe(200)
      await expect(page.getByText('Invite revoked', { exact: true })).toHaveCount(1)
      await expect(inviteRow(toRevoke, listed)).toContainText('Revoked')
      await expect(inviteRow(toRevoke, listed).getByRole('button')).toHaveCount(0)
      await expect(page.getByRole('heading', { name: 'Invites', exact: true })).toBeFocused()
    })
    await capture('invite-revoked')

    await step('the admin logs out', signOut)

    await step('user@example.com opens the revoked link with no session, and signs in', async () => {
      const toLogin = page.waitForRequest((r) => new URL(r.url()).pathname === '/bff/login')
      await page.goto(toRevoke.link)
      const request = await toLogin
      expect(new URL(request.url()).searchParams.get('returnUrl')).toBe('/join')
      const preview = answered('POST', /^\/api\/v1\/invites\/preview$/)
      await signIn('user@example.com')
      expect((await preview).status()).toBe(410)
    })

    await step('the revoked link shows the message of the Server and no form', async () => {
      await expect(page).toHaveURL(/\/join$/)
      await expect(page.getByRole('alert')).toHaveText('This invite was used or revoked. Ask a tenant admin for a new invite.')
      await expect(page.getByRole('form', { name: 'Join the tenant' })).toHaveCount(0)
      await expect(page.getByRole('link', { name: 'Start a tenant' })).toHaveCount(0)
      expectNoTokenInAUrl(...tokens)
    })
    await capture('join-revoked')

    await step('the user logs out from the home page that the message links to', async () => {
      await page.getByRole('link', { name: 'Go to the home page', exact: true }).click()
      await expect(page).toHaveURL(new RegExp(`${new URL(stack.urls.web).origin}/$`))
      await signOut()
    })

    await step('the user opens the good link with no session, and signs in', async () => {
      const toLogin = page.waitForRequest((r) => new URL(r.url()).pathname === '/bff/login')
      await page.goto(claimed.link)
      const request = await toLogin
      expect(new URL(request.url()).searchParams.get('returnUrl')).toBe('/join')
      const preview = answered('POST', /^\/api\/v1\/invites\/preview$/)
      await signIn('user@example.com')
      expect((await preview).status()).toBe(200)
    })

    const preview = await step('the page shows what the user joins, and the token is out of the address bar', async () => {
      const previewed = answered('POST', /^\/api\/v1\/invites\/preview$/)
      await page.reload()
      const res = await previewed
      expect(res.status()).toBe(200)
      expect(JSON.parse(res.request().postData()).token).toBe(claimed.token)
      const body = await res.json()
      await expect(page).toHaveURL(/\/join$/)
      const details = page.getByRole('region', { name: 'Invite' })
      await expect(details).toContainText(body.tenantName)
      await expect(details).toContainText(body.role)
      await expect(page.getByRole('link', { name: 'Start a tenant' })).toHaveCount(0)
      expectNoTokenInAUrl(...tokens)
      return body
    })
    await capture('join-preview')

    await step('a second link in the same tab shows its invite: the email of the account is read-only', async () => {
      const previewed = answered('POST', /^\/api\/v1\/invites\/preview$/)
      await page.goto(matching.link)
      expect((await previewed).status()).toBe(200)
      const form = page.getByRole('form', { name: 'Join the tenant' })
      await expect(form.getByRole('textbox', { name: 'Email' })).not.toBeEditable()
      await expect(form.getByText('so the app uses the email of your account')).toBeVisible()
      await expect(page).toHaveURL(/\/join$/)
    })
    await capture('join-email-match')

    const form = page.getByRole('form', { name: 'Join the tenant' })
    await step('the first link again: the form has the values of the account, and the user changes them', async () => {
      const previewed = answered('POST', /^\/api\/v1\/invites\/preview$/)
      await page.goto(claimed.link)
      expect((await previewed).status()).toBe(200)
      await expect(form.getByRole('textbox', { name: 'Email' })).toBeEditable()
      await expect(form.getByRole('textbox', { name: 'Username' })).not.toHaveValue('')
      await fillPerson(form, {
        firstName: 'Casey',
        lastName: `Colleague ${stamp}`,
        email: `casey-${stamp}@example.com`,
        username: `casey-${stamp}`,
      })
    })
    await capture('join-form-filled')

    const user = await step('Join tenant: the Server adds the user to the tenant', async () => {
      const claim = answered('POST', /^\/api\/v1\/invites\/claim$/)
      await form.getByRole('button', { name: 'Join tenant', exact: true }).click()
      const res = await claim
      expect(res.status()).toBe(201)
      expect(JSON.parse(res.request().postData()).token).toBe(claimed.token)
      return res.json()
    })

    await step('the user lands in the tenant, with one toast that names it', async () => {
      await expect(page).toHaveURL(new RegExp(`${new URL(stack.urls.web).origin}/$`))
      await expect(page.getByText(`You joined ${preview.tenantName}.`, { exact: true })).toHaveCount(1)
      expect(user.tenantId).toBe(tenant.id)
      expect(await page.evaluate(() => sessionStorage.getItem('invite-token'))).toBeNull()
      await expect(page.getByRole('link', { name: 'Start a tenant' })).toHaveCount(0)
      expectNoTokenInAUrl(...tokens)
    })
    await capture('joined-home')

    await step('a member who opens a link gets the message, with no form', async () => {
      await page.goto(matching.link)
      await expect(page.getByText('Your account already belongs to a tenant.', { exact: true })).toBeVisible()
      await expect(page.getByRole('form', { name: 'Join the tenant' })).toHaveCount(0)
      await expect(page).toHaveURL(/\/join$/)
    })
    await capture('join-member')

    await step('a plain member sees no invite button, and sends no request for the invites', async () => {
      const invitesRequests = []
      page.on('request', (r) => {
        if (new URL(r.url()).pathname === '/api/v1/invites') invitesRequests.push(r.url())
      })
      const tenantRead = answered('GET', new RegExp(`^/api/v1/tenants/${tenant.id}$`))
      await page.goto('/users')
      const res = await tenantRead
      expect(res.status()).toBe(200)
      expect((await res.json()).allowedActions).toEqual([])
      await expect(page.getByRole('heading', { name: 'Users', exact: true })).toBeVisible()
      // The list of the users gives a 403 to a role with no read_users. The queries retry one time before the alert shows.
      await expect(page.getByRole('alert').filter({ hasText: 'The users did not load.' })).toBeVisible()
      await expect(page.getByRole('button', { name: 'Invite a user', exact: true })).toHaveCount(0)
      await expect(page.getByRole('heading', { name: 'Invites', exact: true })).toHaveCount(0)
      expect(invitesRequests).toEqual([])
    })
    await capture('users-plain-member')

    await step('the user logs out, and the admin signs in', async () => {
      await signOut()
      await signIn(ADMIN)
    })

    await step('the admin sees the first invite as claimed and the second as revoked', async () => {
      const list = answered('GET', /^\/api\/v1\/invites$/)
      await page.goto('/users')
      const invites = await (await list).json()
      const state = (invite) => invites.find((i) => i.id === invite.id).status
      expect([state(claimed), state(toRevoke), state(matching)]).toEqual(['Claimed', 'Revoked', 'Pending'])
      await expect(inviteRow(claimed, invites)).toContainText('Claimed')
      await expect(inviteRow(claimed, invites).getByRole('button')).toHaveCount(0)
      await expect(inviteRow(toRevoke, invites)).toContainText('Revoked')
      await expect(page.getByRole('grid', { name: 'Users' }).or(page.getByRole('table', { name: 'Users' }))).toContainText(user.fullName)
    })
    await capture('invites-claimed')
  } finally {
    await step('delete the tenant of the run as its admin, so the cache of the Server has no stale tenant', async () => {
      if (signedIn !== ADMIN) {
        if (signedIn) await signOut()
        else await page.goto('/')
        await signIn(ADMIN)
      }
      const status = await page.evaluate(
        async (id) => (await fetch(`/api/v1/tenants/${id}`, { method: 'DELETE', headers: { 'X-CSRF': '1' } })).status,
        tenant.id,
      )
      expect(status).toBe(204)
    })
  }
}
