// Feature: login-logout. BFF login through the IdP of the stack. See features/login-logout.md.
export default async ({ page, context, step, capture, expect, login, logout }) => {
  await step('anonymous: /bff/user is 204 and the page shows Login', async () => {
    const res = page.waitForResponse((r) => r.url().endsWith('/bff/user'))
    await page.goto('/')
    expect((await res).status()).toBe(204)
    await expect(page.getByRole('button', { name: 'Login' })).toBeVisible()
  })
  await capture('anonymous')

  const { name } = await step('log in at the IdP', () => login())

  await step('logged in: the page greets the user by the name claim and the BFF holds a session cookie', async () => {
    await expect(page.getByText(`Welcome, ${name}`)).toBeVisible()
    const cookies = await context.cookies()
    expect(cookies.some((c) => c.name === '__Host-bff')).toBe(true)
  })
  await capture('logged-in')

  await step('session survives a reload', async () => {
    await page.reload()
    await expect(page.getByText(`Welcome, ${name}`)).toBeVisible()
  })

  await step('log out', () => logout())

  await step('logged out: /bff/user is 204 again', async () => {
    const status = await page.evaluate(async () => (await fetch('/bff/user', { headers: { 'X-CSRF': '1' } })).status)
    expect(status).toBe(204)
    await expect(page.getByText(`Welcome, ${name}`)).toHaveCount(0)
  })
  await capture('logged-out')
}
