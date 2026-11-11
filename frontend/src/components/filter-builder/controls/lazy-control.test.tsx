import { Suspense } from 'react'
import { expect, test } from 'vitest'
import { page } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import type { FilterControlProps } from '../types'
import { lazyControl } from './lazy-control'

const config = { propertyKey: 'dueDate', propertyLabel: 'Due Date', controlType: 'date' } as const

function Loaded({ config }: FilterControlProps) {
  return (
    <label>
      The {config.propertyLabel} control <input />
    </label>
  )
}

/** A load of the code that fails as a lost connection does, until the test reconnects. */
function codeOnAFlakyConnection() {
  let connected = false
  let loads = 0
  return {
    load: async () => {
      loads++
      if (!connected) throw new TypeError('Failed to fetch dynamically imported module: http://localhost/assets/date-filter-1.js')
      return Loaded
    },
    reconnect: () => {
      connected = true
    },
    disconnect: () => {
      connected = false
    },
    loads: () => loads,
  }
}

function show(Control: ReturnType<typeof lazyControl>) {
  return render(
    <Suspense>
      <Control config={config} surface="submenu" onSubmit={() => {}} />
    </Suspense>,
  )
}

test('shows the error in place of the control, and loads the code again on Try again', async () => {
  // Arrange
  const code = codeOnAFlakyConnection()
  const Control = lazyControl(code.load)
  await show(Control)
  await expect.element(page.getByText('This filter did not load.')).toBeVisible()

  // Act
  code.reconnect()
  await page.getByRole('button', { name: 'Try again' }).click()

  // Assert
  await expect.element(page.getByText('The Due Date control')).toBeVisible()
  await expect.element(page.getByText('This filter did not load.')).not.toBeInTheDocument()
})

test('keeps the loaded code for each control that shows later', async () => {
  // Arrange
  const code = codeOnAFlakyConnection()
  code.reconnect()
  const Control = lazyControl(code.load)
  const first = await show(Control)
  await expect.element(page.getByText('The Due Date control')).toBeVisible()
  await first.unmount()

  // Act
  await show(Control)

  // Assert
  await expect.element(page.getByText('The Due Date control')).toBeVisible()
  expect(code.loads()).toBe(1)
})

test('keeps a loaded control and what the user typed in it when Try again fails in another control', async () => {
  // Arrange
  const code = codeOnAFlakyConnection()
  const Control = lazyControl(code.load)
  const twoControls = (surface: FilterControlProps['surface']) => (
    <Suspense>
      {['Due Date', 'Start Date'].map((propertyLabel) => (
        <section key={propertyLabel} aria-label={propertyLabel}>
          <Control config={{ ...config, propertyLabel }} surface={surface} onSubmit={() => {}} />
        </section>
      ))}
    </Suspense>
  )
  const screen = await render(twoControls('submenu'))
  const section = (name: string) => page.getByRole('region', { name })
  await expect.element(section('Start Date').getByText('This filter did not load.')).toBeVisible()
  code.reconnect()
  await section('Due Date').getByRole('button', { name: 'Try again' }).click()
  await section('Due Date').getByRole('textbox').fill('kept')
  code.disconnect()

  // Act
  await section('Start Date').getByRole('button', { name: 'Try again' }).click()
  await screen.rerender(twoControls('dialog'))

  // Assert
  await expect.element(section('Start Date').getByText('This filter did not load.')).toBeVisible()
  await expect.element(section('Due Date').getByRole('textbox')).toHaveValue('kept')
})
