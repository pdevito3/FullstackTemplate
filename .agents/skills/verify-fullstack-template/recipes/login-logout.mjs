// Feature: login-logout. BFF login through the IdP of the stack. See features/login-logout.md.
// The page is on the IdP when the visitor has no session, so a check of the app sends its own request. The context
// sends the cookies of the app with it.
const appStatus = async (page, path) => (await page.request.get(path, { headers: { 'X-CSRF': '1' } })).status()

export default async ({ page, context, stack, step, capture, expect, login, logout }) => {
  await step('anonymous: /bff/user is 204, the SPA goes to the login at once with / as the return URL, and the Server answers 401', async () => {
    const res = page.waitForResponse((r) => r.url().endsWith('/bff/user'))
    const loginRequest = page.waitForRequest((r) => new URL(r.url()).pathname === '/bff/login')
    await page.goto('/', { waitUntil: 'commit' })
    expect((await res).status()).toBe(204)
    expect(new URL((await loginRequest).url()).searchParams.get('returnUrl')).toBe('/')
    // The dev build goes to the login twice, and the first navigation aborts. A waitForURL can fail on that abort.
    await expect.poll(() => new URL(page.url()).origin).not.toBe(new URL(stack.urls.web).origin)
    await page.waitForLoadState('domcontentloaded')
    expect(await appStatus(page, '/api/v1/users/me')).toBe(401)
  })
  await capture('anonymous')

  const { name } = await step('log in at the IdP', () => login())

  await step('logged in: the page shows the name that the Server read from the token, and the BFF holds a session cookie', async () => {
    await expect(page.getByText(`Signed in as ${name}`)).toBeVisible()
    const me = await page.evaluate(async () => {
      const response = await fetch('/api/v1/users/me', { headers: { 'X-CSRF': '1' } })
      return { status: response.status, body: await response.json() }
    })
    expect(me.status).toBe(200)
    expect(me.body.name).toBe(name)
    const cookies = await context.cookies()
    expect(cookies.some((c) => c.name === '__Host-bff')).toBe(true)
  })
  await capture('logged-in')

  await step('session survives a reload', async () => {
    await page.reload()
    await expect(page.getByText(`Signed in as ${name}`)).toBeVisible()
  })

  await step('log out', () => logout())

  await step('logged out: /bff/user is 204 again, and the page is the login of the IdP', async () => {
    expect(await appStatus(page, '/bff/user')).toBe(204)
    await expect(page.getByText(`Signed in as ${name}`)).toHaveCount(0)
  })
  await capture('logged-out')
}
