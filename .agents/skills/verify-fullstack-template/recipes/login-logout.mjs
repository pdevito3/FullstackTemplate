// Feature: login-logout. BFF login through the IdP of the stack. See features/login-logout.md.
export default async ({ page, context, step, capture, expect, login, logout }) => {
  await step('anonymous: /bff/user is 204, the page shows Login, and the Server answers 401', async () => {
    const res = page.waitForResponse((r) => r.url().endsWith('/bff/user'))
    await page.goto('/')
    expect((await res).status()).toBe(204)
    await expect(page.getByRole('button', { name: 'Login' })).toBeVisible()
    const status = await page.evaluate(async () => (await fetch('/api/v1/users/me', { headers: { 'X-CSRF': '1' } })).status)
    expect(status).toBe(401)
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

  await step('logged out: /bff/user is 204 again', async () => {
    const status = await page.evaluate(async () => (await fetch('/bff/user', { headers: { 'X-CSRF': '1' } })).status)
    expect(status).toBe(204)
    await expect(page.getByText(`Signed in as ${name}`)).toHaveCount(0)
  })
  await capture('logged-out')
}
