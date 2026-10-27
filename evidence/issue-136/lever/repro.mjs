// Usage: node repro.mjs <frontendDir> <baseURL> <outDir>
// Drives the Between calendar of /filter-demo and prints one JSON line for each scenario and viewport.
import { createRequire } from 'node:module'
import { mkdirSync, writeFileSync } from 'node:fs'
import path from 'node:path'

const [frontendDir, baseURL, outDir] = process.argv.slice(2)
const require = createRequire(path.join(frontendDir, 'package.json'))
const { chromium } = require('@playwright/test')
mkdirSync(outDir, { recursive: true })

const viewports = [
  { width: 320, height: 900 },
  { width: 768, height: 1024 },
  { width: 1280, height: 900 },
]
const rangeChips = ['This Week', 'Last Week', 'Next Week', 'This Month', 'Last Month', 'Next Month', 'This Year', 'Last Year']
const singleChips = ['Today', 'Tomorrow', 'Yesterday']

const browser = await chromium.launch()

// Two frames in a row that match, so that an animation in flight does not count as a change.
async function steadyShot(page, file) {
  let last
  for (let i = 0; i < 30; i++) {
    const shot = await page.screenshot({ animations: 'disabled' })
    if (last && shot.equals(last)) { writeFileSync(file, shot); return }
    last = shot
    await page.waitForTimeout(150)
  }
  throw new Error(`no steady frame for ${file}`)
}
const results = []

async function openControl(page, between = true) {
  await page.clock.setFixedTime(new Date('2026-08-15T12:00:00Z'))
  await page.goto(`${baseURL}/filter-demo`)
  await page.getByRole('heading', { name: 'Filter Builder Demo' }).waitFor()
  await page.getByRole('button', { name: 'Add Filter', exact: true }).click()
  await page.getByRole('menuitem', { name: 'Created Date', exact: true }).click()
  const control = page.getByRole('dialog', { name: 'Created Date filter' })
  await control.getByRole('grid').waitFor()
  if (between) {
    await control.getByRole('button', { name: 'Between', exact: true }).click()
    await control.getByRole('group', { name: 'Start date', exact: true }).waitFor()
  }
  return control
}

async function read(page, control) {
  return page.evaluate(() => document.activeElement?.getAttribute('aria-label') ?? document.activeElement?.tagName).then(async (focus) => ({
    focus,
    month: (await control.getByRole('button', { name: 'Select month' }).textContent()).trim(),
    start: (await control.getByRole('group', { name: 'Start date', exact: true }).textContent()).trim(),
    end: (await control.getByRole('group', { name: 'End date', exact: true }).textContent()).trim(),
  }))
}

const scenarios = {
  async keyboardTwoMonths(page, control) {
    await control.getByRole('button', { name: /August 15, 2026/ }).focus()
    await page.keyboard.press('Enter')
    for (const key of ['ArrowDown', 'ArrowDown', 'ArrowRight', 'ArrowRight', 'ArrowRight', 'ArrowRight', 'ArrowRight']) await page.keyboard.press(key)
    await page.keyboard.press('Enter')
  },
  async keyboardOneMonth(page, control) {
    await control.getByRole('button', { name: /August 15, 2026/ }).focus()
    await page.keyboard.press('Enter')
    for (const key of ['ArrowRight', 'ArrowRight', 'ArrowRight']) await page.keyboard.press(key)
    await page.keyboard.press('Enter')
  },
  async keyboardBackward(page, control) {
    await control.getByRole('button', { name: /August 15, 2026/ }).focus()
    await page.keyboard.press('Enter')
    for (const key of ['ArrowUp', 'ArrowUp', 'ArrowUp']) await page.keyboard.press(key)
    await page.keyboard.press('Enter')
  },
  async mouseTwoMonths(page, control) {
    await control.getByRole('button', { name: /August 15, 2026/ }).click()
    await control.locator('button[slot="next"]').click()
    await control.getByRole('button', { name: /September 3, 2026/ }).click()
  },
  async mouseOneMonth(page, control) {
    await control.getByRole('button', { name: /August 15, 2026/ }).click()
    await control.getByRole('button', { name: /August 18, 2026/ }).click()
  },
  async typedStart(page, control) {
    await control.getByRole('button', { name: 'Last Year', exact: true }).click()
    await control.getByRole('group', { name: 'Start date', exact: true }).getByRole('spinbutton').first().click()
    await page.keyboard.type('03102025')
  },
  async typedEnd(page, control) {
    await control.getByRole('button', { name: 'Last Year', exact: true }).click()
    await control.getByRole('group', { name: 'End date', exact: true }).getByRole('spinbutton').first().click()
    await page.keyboard.type('11202025')
  },
}

for (const viewport of viewports) {
  const tag = `${viewport.width}`
  for (const [name, run] of Object.entries(scenarios).filter(([n]) => !process.env.ONLY || n === process.env.ONLY)) {
    const context = await browser.newContext({ viewport, locale: 'en-US', timezoneId: 'UTC', reducedMotion: 'reduce' })
    const page = await context.newPage()
    const errors = []
    page.on('pageerror', (e) => errors.push(String(e)))
    await page.route((url) => url.pathname.startsWith('/bff/') || url.pathname.startsWith('/api/'), (route) =>
      route.fulfill(new URL(route.request().url()).pathname === '/bff/user' ? { status: 204 } : { status: 501 }))
    const control = await openControl(page)
    await steadyShot(page, path.join(outDir, `${tag}-${name}-0-before.png`))
    await run(page, control)
    await page.waitForTimeout(300)
    const state = await read(page, control)
    await steadyShot(page, path.join(outDir, `${tag}-${name}-1-after.png`))
    results.push({ viewport: tag, scenario: name, ...state, errors })
    await context.close()
  }
  for (const [between, chips] of process.env.ONLY ? [] : [[true, rangeChips], [false, singleChips]]) {
    const context = await browser.newContext({ viewport, locale: 'en-US', timezoneId: 'UTC', reducedMotion: 'reduce' })
    const page = await context.newPage()
    await page.route((url) => url.pathname.startsWith('/bff/') || url.pathname.startsWith('/api/'), (route) =>
      route.fulfill(new URL(route.request().url()).pathname === '/bff/user' ? { status: 204 } : { status: 501 }))
    const control = await openControl(page, between)
    const months = {}
    for (const chip of chips) {
      await control.getByRole('button', { name: chip, exact: true }).click()
      await page.waitForTimeout(100)
      months[chip] = (await control.getByRole('button', { name: 'Select month' }).textContent()).trim()
    }
    results.push({ viewport: tag, scenario: between ? 'rangeChips' : 'singleChips', months })
    await context.close()
  }
}

await browser.close()
writeFileSync(path.join(outDir, 'results.json'), JSON.stringify(results, null, 2))
for (const r of results) console.log(JSON.stringify(r))
