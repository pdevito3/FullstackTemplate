// Feature: start-tenant. A signed-in user with no user record starts a tenant and becomes its admin. See features/start-tenant.md.
const api = (page, method, path) =>
  page.evaluate(async ([method, path]) => {
    const r = await fetch(path, { method, headers: { 'X-CSRF': '1' } })
    return { status: r.status, body: r.status === 204 ? null : await r.json() }
  }, [method, path])

export default async ({ page, step, capture, expect, login }) => {
  await step('log in', () => login())
  const claims = await step('the Server has no user record for the new user', async () => {
    const body = (await api(page, 'GET', '/bff/user')).body
    const claim = (type) => body.find((c) => c.type === type)?.value
    expect((await api(page, 'GET', `/api/v1/users/by-identifier/${encodeURIComponent(claim('sub'))}`)).status).toBe(404)
    return { sub: claim('sub'), givenName: claim('given_name'), familyName: claim('family_name'), email: claim('email') }
  })

  const notice = page.getByRole('region', { name: 'No tenant' })
  await step('the home page shows the notice', async () => {
    await expect(notice).toContainText('You are not a member of a tenant.')
  })
  await capture('home-with-notice')

  await step('a refused lookup shows the reason and Try Again, and Try Again gets the answer of the Server', async () => {
    const reason = 'You are not a member of a tenant. Ask a tenant admin to add you.'
    await page.route('**/api/v1/users/by-identifier/**', (route) => route.fulfill({
      status: 403,
      contentType: 'application/problem+json',
      body: JSON.stringify({ type: 'https://tools.ietf.org/html/rfc9110#section-15.5.4', status: 403, title: 'Forbidden', detail: reason }),
    }), { times: 1 })
    await page.reload()
    const banner = page.getByRole('region', { name: 'Membership check' })
    await expect(banner).toContainText(reason)
    await banner.getByRole('button', { name: 'Try Again' }).click()
    await expect(notice.getByRole('link', { name: 'Start a tenant' })).toBeFocused()
  })

  const form = page.getByRole('form', { name: 'Start a tenant' })
  await step('follow Start a tenant: the form has the name and email of the identity provider', async () => {
    await notice.getByRole('link', { name: 'Start a tenant' }).click()
    await expect(page.getByRole('heading', { name: 'Start a tenant', level: 1 })).toBeVisible()
    await expect(form.getByLabel('Tenant name', { exact: true })).toBeFocused()
    await expect(form.getByLabel('First name', { exact: true })).toHaveValue(claims.givenName)
    await expect(form.getByLabel('Last name', { exact: true })).toHaveValue(claims.familyName)
    await expect(form.getByLabel('Email', { exact: true })).toHaveValue(claims.email)
  })
  await capture('start-tenant-form')

  await step('an empty tenant name is refused in the form', async () => {
    await form.getByRole('button', { name: 'Start a tenant' }).click()
    await expect(form.getByRole('alert')).toHaveText('Enter a tenant name.')
  })

  const tenantName = `Verify ${Date.now()}`
  const started = await step('start the tenant: POST /api/v1/users/initiate is 201 with the role Admin', async () => {
    await form.getByLabel('Tenant name', { exact: true }).fill(tenantName)
    const res = page.waitForResponse((r) => r.request().method() === 'POST' && r.url().endsWith('/api/v1/users/initiate'))
    await form.getByRole('button', { name: 'Start a tenant' }).click()
    const r = await res
    expect(r.status()).toBe(201)
    const body = await r.json()
    expect(body.user.identifier).toBe(claims.sub)
    expect(body.user.role).toBe('Admin')
    return body
  })

  const homeLink = page.getByRole('main').getByRole('link', { name: 'Go to the home page' })
  await step('the page names the new tenant, and the focus is on Go to the home page', async () => {
    await expect(page.getByText(`You started ${started.tenant.name}. You are its admin`, { exact: false })).toBeVisible()
    await expect(homeLink).toBeFocused()
  })
  await capture('started')

  await step('go to the home page: the page heading has the focus, and the notice is gone', async () => {
    await homeLink.click()
    await expect(page).toHaveURL(/\/$/)
    await expect(page.getByRole('heading', { level: 1 })).toBeFocused()
    await expect(notice).toHaveCount(0)
  })
  await capture('home-after-start')

  await step('the new admin holds the permissions of the role: GET /api/v1/users is 200 and lists the admin', async () => {
    const users = await api(page, 'GET', '/api/v1/users')
    expect(users.status).toBe(200)
    expect(users.body.map((u) => u.id)).toEqual([started.user.id])
    expect(started.user.permissions).toContain('manage_users')
  })
}
