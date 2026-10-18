// Feature: login-logout. BFF login through the Duende demo IdP. See features/login-logout.md.
export default async ({ page, context, step, capture, expect, login, logout }) => {
  await step('anonymous: /bff/user is 401 and the page shows Login', async () => {
    const res = page.waitForResponse((r) => r.url().endsWith('/bff/user'))
    await page.goto('/')
    expect((await res).status()).toBe(401)
    await expect(page.getByRole('button', { name: 'Login' })).toBeVisible()
  })
  await capture('anonymous')

  await step('log in as bob at the Duende demo IdP', () => login('bob'))

  await step('logged in: the page greets Bob Smith and the BFF holds a session cookie', async () => {
    await expect(page.getByText('Welcome, Bob Smith')).toBeVisible()
    const cookies = await context.cookies()
    expect(cookies.some((c) => c.name === '__Host-bff')).toBe(true)
    const claims = await page.evaluate(async () => (await fetch('/bff/user', { headers: { 'X-CSRF': '1' } })).json())
    expect(claims.find((c) => c.type === 'name')?.value).toBe('Bob Smith')
  })
  await capture('logged-in')

  await step('session survives a reload', async () => {
    await page.reload()
    await expect(page.getByText('Welcome, Bob Smith')).toBeVisible()
  })

  await step('log out', () => logout())

  await step('logged out: /bff/user is 401 again', async () => {
    const status = await page.evaluate(async () => (await fetch('/bff/user', { headers: { 'X-CSRF': '1' } })).status)
    expect(status).toBe(401)
    await expect(page.getByText('Welcome, Bob Smith')).toHaveCount(0)
  })
  await capture('logged-out')
}
