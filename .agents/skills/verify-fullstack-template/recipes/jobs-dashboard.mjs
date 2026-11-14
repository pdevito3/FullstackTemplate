// Feature: jobs-dashboard. The BackWave dashboard at /backwave, for operators only. See features/jobs-dashboard.md.
// The dashboard is a page of the Server, not of the SPA. The BFF forwards /backwave with the token of the session.
// BackWave makes the pages of the dashboard, so their captures get no sweep. The captures of the SPA get it.
const api = (page, method, path) =>
  page.evaluate(async ([method, path]) => {
    const r = await fetch(path, { method, headers: { 'X-CSRF': '1' } })
    return { status: r.status, body: r.status === 204 ? null : await r.json() }
  }, [method, path])
const isLive = (r) => {
  const url = new URL(r.url())
  return url.pathname.startsWith('/backwave') && url.searchParams.get('live') === '1'
}

export default async ({ page, step, capture, expect, login, logout, stack }) => {
  await step('anonymous: /backwave goes to the login of the IdP, and the login comes back to the dashboard', async () => {
    await page.goto('/backwave', { waitUntil: 'commit' })
    await page.waitForURL((u) => u.origin !== new URL(stack.urls.web).origin, { waitUntil: 'domcontentloaded' })
    if (stack.authProvider.toLowerCase() !== 'zerofactor') {
      // login() expects the SPA after the login. For another IdP, sign in on its form here.
      throw new Error(`The recipe signs in at ZeroFactor only. The stack uses ${stack.authProvider}.`)
    }
    const live = page.waitForResponse(isLive)
    await page.locator('[data-testid="user-admin@example.com"]').click({ timeout: 30_000 })
    await page.waitForURL((u) => u.origin === new URL(stack.urls.web).origin && u.pathname === '/backwave')
    await expect(page.getByRole('heading', { name: 'Overview', level: 1 })).toBeVisible()
    const stream = await live
    expect(stream.status()).toBe(200)
    expect(stream.headers()['content-type']).toContain('text/event-stream')
  })
  await capture('dashboard-overview', { sweep: false })

  await step('operator: a link on another site opens the dashboard with the session', async () => {
    // The browser does not send the SameSite=Strict session cookie on a cross-site navigation, so the BFF answers with
    // a page that loads the same URL again from the app
    const target = new URL('/backwave/queues', stack.urls.web).href
    await page.route('http://mail.example.test/**', (route) =>
      route.fulfill({ contentType: 'text/html', body: `<!DOCTYPE html><title>Mail</title><a href="${target}">Open the queues</a>` }),
    )
    await page.goto('http://mail.example.test/inbox')
    const loads = []
    const onRequest = (r) => r.frame() === page.mainFrame() && r.url() === target && loads.push(r)
    page.on('request', onRequest)
    await page.getByRole('link', { name: 'Open the queues' }).click()
    await expect(page.getByRole('heading', { name: 'Queues', level: 1 })).toBeVisible()
    page.off('request', onRequest)
    // The response bodies are gone after the navigation, so the cookie of each load is the proof
    const hasSession = async (r) => /(^|; )__Host-bff=/.test((await r.allHeaders()).cookie ?? '')
    expect(loads).toHaveLength(2)
    expect(await hasSession(loads[0])).toBe(false)
    expect((await loads[0].response()).status()).toBe(200)
    expect(await hasSession(loads[1])).toBe(true)
    await page.unroute('http://mail.example.test/**')
  })
  await capture('cross-site-queues', { sweep: false })

  // The job store keeps the jobs of earlier runs, so the recipe finds its own job by the tenant tag
  const tenant = await step('operator: the delete of a new tenant queues delete-tenant-objects, and the dashboard shows it Succeeded', async () => {
    await page.goto('/')
    await page.getByRole('region', { name: 'No tenant' }).getByRole('link', { name: 'Start a tenant' }).click()
    const form = page.getByRole('form', { name: 'Start a tenant' })
    await form.getByLabel('Tenant name', { exact: true }).fill(`Jobs ${Date.now().toString(36)}`)
    const started = page.waitForResponse((r) => r.request().method() === 'POST' && r.url().endsWith('/api/v1/users/initiate'))
    await form.getByRole('button', { name: 'Start a tenant' }).click()
    expect((await started).status()).toBe(201)
    const { tenant } = await (await started).json()
    expect((await api(page, 'DELETE', `/api/v1/tenants/${tenant.id}`)).status).toBe(204)
    await expect.poll(async () => {
      await page.goto(`/backwave/jobs?wire=delete-tenant-objects&state=Succeeded&tk=tenant&tv=${encodeURIComponent(tenant.id)}`)
      return page.locator('tbody tr').count()
    }, { timeout: 30_000 }).toBe(1)
    return tenant
  })
  await capture('tenant-job-succeeded', { sweep: false })

  await step('operator: the page of the job gives its state and its tenant', async () => {
    await page.locator('tbody tr').first().getByRole('link').first().click()
    await expect(page.getByRole('heading', { name: /^Job /, level: 1 })).toBeVisible()
    await expect(page.getByText('Succeeded').first()).toBeVisible()
    await expect(page.getByText(tenant.id).first()).toBeVisible()
  })
  await capture('tenant-job', { sweep: false })

  await step('operator: the sidebar of the SPA has Background jobs, and it opens the dashboard in the same tab', async () => {
    await page.goto('/')
    await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible()
    const link = page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Background jobs' })
    await expect(link).toHaveAttribute('href', '/backwave')
    await expect(link).not.toHaveAttribute('target', /.+/)
    const me = await page.evaluate(async () => (await fetch('/api/v1/users/me', { headers: { 'X-CSRF': '1' } })).json())
    expect(me.canOpenJobDashboard).toBe(true)
  })
  await capture('operator-sidebar')

  await step('operator: a click on Background jobs loads the dashboard, and its queues page lists the object deletes', async () => {
    await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Background jobs' }).click()
    await page.waitForURL((u) => u.pathname === '/backwave')
    await expect(page.getByRole('heading', { name: 'Overview', level: 1 })).toBeVisible()
    await page.goto('/backwave/queues')
    await expect(page.getByRole('heading', { name: 'Queues', level: 1 })).toBeVisible()
    await expect(page.getByText('object-deletes', { exact: true }).first()).toBeVisible()
    // A pause holds the jobs of all tenants, so the Server denies it and the page shows no button for it
    await expect(page.getByRole('button', { name: /pause/i })).toHaveCount(0)
  })
  await capture('dashboard-queues', { sweep: false })

  await step('log out the operator', async () => {
    await page.goto('/')
    await logout()
  })

  await step('member: the sidebar has no Background jobs, and /backwave answers 403', async () => {
    await login('user@example.com')
    const nav = page.getByRole('navigation', { name: 'Main' })
    await expect(nav.getByRole('link', { name: 'Home' })).toBeVisible()
    const me = await page.evaluate(async () => (await fetch('/api/v1/users/me', { headers: { 'X-CSRF': '1' } })).json())
    expect(me.canOpenJobDashboard).toBe(false)
    await expect(nav.getByRole('link', { name: 'Background jobs' })).toHaveCount(0)
    const res = await page.request.get('/backwave')
    expect(res.status()).toBe(403)
  })
  await capture('member-sidebar')
}
