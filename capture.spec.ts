import { test, expect } from '@tests/support/fixtures'
import type { Page } from '@playwright/test'
import { appendFileSync, mkdirSync } from 'node:fs'

const outDir = process.env.EVIDENCE_DIR!
mkdirSync(outDir, { recursive: true })
const aug15 = new Date('2026-08-15T12:00:00Z')
const sep15 = new Date('2026-09-15T12:00:00Z')

const viewports = [
  { name: '320', size: { width: 320, height: 780 } },
  { name: '768', size: { width: 768, height: 1024 } },
  { name: '1280', size: { width: 1280, height: 900 } },
]

const control = (page: Page) => page.getByRole('dialog', { name: /Created Date filter$/i }).last()
const heading = (page: Page) => control(page).getByRole('button', { name: 'Select month' })

async function open(page: Page, today = aug15) {
  await page.clock.setFixedTime(today)
  await page.goto('/filter-demo')
  await expect(page.getByRole('heading', { name: 'Filter Builder Demo' })).toBeVisible()
  await page.getByRole('button', { name: 'Add Filter', exact: true }).click()
  await page.getByRole('menuitem', { name: 'Created Date', exact: true }).click()
  await expect(control(page).getByRole('grid')).toBeVisible()
}

async function between(page: Page) {
  await control(page).getByRole('button', { name: 'Between', exact: true }).click()
  await expect(control(page).getByRole('group', { name: 'Start date', exact: true })).toBeVisible()
}

async function nav(page: Page, name: 'Next' | 'Previous', expected: string) {
  await control(page).getByRole('banner').getByRole('button', { name, exact: true }).click()
  await expect(heading(page)).toHaveText(expected)
}

async function monthPicker(page: Page) {
  await heading(page).click()
  await expect(control(page).getByRole('listbox', { name: 'Month picker' })).toBeVisible()
}

async function yearPicker(page: Page) {
  await monthPicker(page)
  await control(page).getByRole('button', { name: 'Select year' }).click()
  await expect(control(page).getByRole('listbox', { name: 'Year picker' })).toBeVisible()
}

async function settle(page: Page) {
  await page.mouse.move(0, 0)
  await page.waitForTimeout(400)
}

async function metrics(page: Page) {
  return control(page).evaluate((d) => {
    const r = (e: Element) => {
      const b = e.getBoundingClientRect()
      return [Math.round(b.top), Math.round(b.bottom), Math.round(b.height)]
    }
    const add = [...d.querySelectorAll('button')].find((b) => b.textContent?.trim() === 'Add Filter')
    const scrollers: string[] = []
    for (let e: Element | null = d; e; e = e.parentElement) {
      const s = getComputedStyle(e)
      if (/(auto|scroll)/.test(s.overflowY) && e.scrollHeight > e.clientHeight)
        scrollers.push(`${e.tagName}:${e.scrollHeight}/${e.clientHeight}`)
    }
    const doc = document.scrollingElement!
    return {
      inner: innerHeight,
      control: r(d),
      add: add ? r(add) : null,
      addInView: add ? add.getBoundingClientRect().bottom <= innerHeight : null,
      scrollers,
      doc: `${doc.scrollHeight}/${doc.clientHeight}`,
      table: d.querySelector('table') ? r(d.querySelector('table')!) : null,
    }
  })
}

const states: Record<string, { today?: Date; act: (page: Page) => Promise<void> }> = {
  'on-aug': { act: async () => {} },
  'on-sep': { act: (page) => nav(page, 'Next', 'September 2026') },
  'on-today-sep': { today: sep15, act: async () => {} },
  'on-feb': {
    act: async (page) => {
      for (const m of ['July', 'June', 'May', 'April', 'March', 'February']) await nav(page, 'Previous', `${m} 2026`)
    },
  },
  'on-next-previous': {
    act: async (page) => {
      await nav(page, 'Next', 'September 2026')
      await nav(page, 'Previous', 'August 2026')
    },
  },
  'on-month-picker': { act: monthPicker },
  'on-today-sep-month-picker': { today: sep15, act: monthPicker },
  'on-year-picker': { act: yearPicker },
  'on-today-sep-year-picker': { today: sep15, act: yearPicker },
  'on-picker-back': {
    act: async (page) => {
      await monthPicker(page)
      await control(page).getByRole('listbox', { name: 'Month picker' }).getByRole('option', { name: 'Aug' }).click()
      await expect(control(page).getByRole('grid')).toBeVisible()
    },
  },
  'between-aug': { act: between },
  'between-today-sep': { today: sep15, act: between },
  'between-sep': {
    act: async (page) => {
      await between(page)
      await nav(page, 'Next', 'September 2026')
    },
  },
  'between-month-picker': {
    act: async (page) => {
      await between(page)
      await monthPicker(page)
    },
  },
  'between-year-picker': {
    act: async (page) => {
      await between(page)
      await yearPicker(page)
    },
  },
  'between-month-pick-then-sep3': {
    act: async (page) => {
      await between(page)
      await control(page).getByRole('button', { name: /August 15, 2026/ }).click()
      await monthPicker(page)
      await control(page).getByRole('listbox', { name: 'Month picker' }).getByRole('option', { name: 'Sep' }).click()
      await control(page).getByRole('button', { name: /September 3, 2026/ }).click()
    },
  },
  'between-typed-start': {
    act: async (page) => {
      await between(page)
      await control(page).getByRole('spinbutton', { name: 'month, Start date' }).click()
      await page.keyboard.type('03102026')
    },
  },
}

const componentStates: Record<string, { today: Date; act: (page: Page) => Promise<void> }> = {
  'components-datepicker-aug': {
    today: aug15,
    act: async (page) => {
      await page.getByRole('group', { name: 'Start date' }).getByRole('button').click()
      await expect(page.getByRole('dialog').getByRole('grid')).toBeVisible()
    },
  },
  'components-datepicker-sep': {
    today: sep15,
    act: async (page) => {
      await page.getByRole('group', { name: 'Start date' }).getByRole('button').click()
      await expect(page.getByRole('dialog').getByRole('grid')).toBeVisible()
    },
  },
  'components-pickadate-sep': {
    today: sep15,
    act: async (page) => {
      await page.getByRole('button', { name: 'Pick a date' }).click()
      await expect(page.getByRole('dialog', { name: 'Pick a date' }).getByRole('grid')).toBeVisible()
    },
  },
  'components-pickadate-sep-month-picker': {
    today: sep15,
    act: async (page) => {
      await page.getByRole('button', { name: 'Pick a date' }).click()
      const d = page.getByRole('dialog', { name: 'Pick a date' })
      await d.getByRole('button', { name: 'Select month' }).click()
      await expect(d.getByRole('listbox', { name: 'Month picker' })).toBeVisible()
    },
  },
}

for (const scheme of ['light', 'dark'] as const) {
  for (const viewport of viewports) {
    test.describe(`${scheme} ${viewport.name}`, () => {
      test.use({ viewport: viewport.size, colorScheme: scheme })
      for (const [name, s] of Object.entries(states)) {
        test(name, async ({ page }) => {
          await open(page, s.today)
          await s.act(page)
          await settle(page)
          const file = `${viewport.name}-${scheme}-${name}`
          await page.screenshot({ path: `${outDir}/${file}.png` })
          appendFileSync(`${outDir}/metrics.tsv`, `${file}\t${JSON.stringify(await metrics(page))}\n`)
        })
      }
      for (const [name, s] of Object.entries(componentStates)) {
        test(name, async ({ page }) => {
          await page.clock.setFixedTime(s.today)
          await page.goto('/components')
          await s.act(page)
          await settle(page)
          const file = `${viewport.name}-${scheme}-${name}`
          await page.screenshot({ path: `${outDir}/${file}.png` })
          const dlg = page.getByRole('dialog').last()
          await dlg.screenshot({ path: `${outDir}/${file}-popover.png` })
          const box = await dlg.boundingBox()
          appendFileSync(`${outDir}/metrics.tsv`, `${file}\t${JSON.stringify(box)}\n`)
        })
      }
    })
  }
}
