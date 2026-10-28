import { mkdirSync, writeFileSync } from 'node:fs'
import path from 'node:path'

const VIEWPORTS = [
  { name: '320', width: 320, height: 800 },
  { name: '375', width: 375, height: 812 },
  { name: '768', width: 768, height: 1024 },
  { name: 'desktop', width: 1440, height: 900 },
]
const THEMES = ['light', 'dark']

export default async ({ page, step, capture, settle, login, expect }) => {
  const outDir = process.env.HEADER_DIR
  mkdirSync(outDir, { recursive: true })
  const metrics = []
  const title = page.getByRole('heading', { name: 'Weather Forecast', level: 1 })
  const header = title.locator('xpath=../..')

  const lines = async (locator) =>
    locator.evaluate((el) => {
      const r = el.getBoundingClientRect()
      const lh = parseFloat(getComputedStyle(el).lineHeight)
      return { lines: Math.round(r.height / lh), x: r.x, y: r.y, w: r.width, h: r.height }
    })

  const shoot = async (auth) => {
    for (const theme of THEMES) {
      await step(`${auth}: choose ${theme} mode`, async () => {
        await page.setViewportSize({ width: 1440, height: 900 })
        await page.getByRole('button', { name: theme === 'dark' ? 'Dark mode' : 'Light mode' }).click()
        await expect(page.locator('html')).toHaveClass(new RegExp(`\\b${theme}\\b`))
      })
      for (const vp of VIEWPORTS) {
        const id = `${auth}-${theme}-${vp.name}`
        await step(`${id}: measure header`, async () => {
          await page.setViewportSize({ width: vp.width, height: vp.height })
          await expect(title).toBeVisible()
          await settle()
          const m = { id, header: await lines(header), title: await lines(title) }
          m.subtitle = await lines(page.getByText('.NET Aspire + React with a BFF'))
          if (auth === 'signed-in') {
            m.welcome = await lines(page.getByText(/^Welcome,/))
            m.logout = await lines(page.getByRole('button', { name: 'Logout' }))
          } else {
            m.login = await lines(page.getByRole('button', { name: 'Login' }))
          }
          metrics.push(m)
          await header.screenshot({ path: path.join(outDir, `${id}-header.png`) })
        })
        await capture(id, { fullPage: false })
      }
    }
  }

  await step('open / signed out', async () => {
    await page.goto('/')
    await expect(page.getByRole('button', { name: 'Login' })).toBeVisible()
  })
  await shoot('signed-out')

  let name
  await step('sign in as admin@example.com', async () => {
    ;({ name } = await login('admin@example.com'))
    await page.goto('/')
    await expect(page.getByText(`Welcome, ${name}`)).toBeVisible()
  })
  await shoot('signed-in')

  writeFileSync(path.join(outDir, 'metrics.json'), JSON.stringify(metrics, null, 2))
}
