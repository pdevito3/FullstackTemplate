// node clickmiss.cjs <baseURL>: type a short year, then click a day or a quick select with a real mouse, and print the field.
const { chromium } = require('/Users/paul/repos/FullstackTemplate-worktrees/issue-145/frontend/node_modules/@playwright/test')
const baseURL = process.argv[2]
;(async () => {
  const browser = await chromium.launch()
  for (const target of [['button', /August 20, 2026/], ['button', 'Yesterday'], ['button', /August 28, 2026/]]) {
    for (const offset of [0.2, 0.5, 0.85]) {
      const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, locale: 'en-US', timezoneId: 'UTC', reducedMotion: 'reduce' })
      await context.route((url) => url.pathname === '/bff/user', (r) => r.fulfill({ status: 204 }))
      const page = await context.newPage()
      await page.clock.setFixedTime(new Date('2026-08-15T12:00:00Z'))
      await page.goto(baseURL + '/filter-demo')
      await page.getByRole('button', { name: 'Add Filter', exact: true }).click()
      await page.getByRole('menuitem', { name: 'Created Date', exact: true }).click()
      const control = page.getByRole('dialog', { name: 'Created Date filter', exact: true })
      await control.getByRole('spinbutton', { name: 'month, Date', exact: true }).click()
      await page.keyboard.type('0310999')
      const el = control.getByRole(target[0], { name: target[1], exact: typeof target[1] === 'string' })
      const box = await el.boundingBox()
      await page.mouse.move(box.x + box.width / 2, box.y + box.height * offset)
      await page.mouse.down()
      await page.mouse.up()
      console.log(String(target[1]), offset, '->', await control.getByRole('group', { name: 'Date', exact: true }).textContent())
      await context.close()
    }
  }
  await browser.close()
})()
