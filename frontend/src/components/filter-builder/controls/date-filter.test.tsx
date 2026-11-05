import { CalendarDateTime } from '@internationalized/date'
import { expect, test, vi } from 'vitest'
import { page, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { browserTimeZone, parseTimeZone } from '@/lib/zoned-time'

import type { DateValue, FilterConfig } from '../types'
import { Operators } from '../utils/operators'
import { DateFilter } from './date-filter'

const pagoPago = parseTimeZone('Pacific/Pago_Pago')
const startsAt: FilterConfig = {
  propertyKey: 'startsAt',
  propertyLabel: 'Starts At',
  controlType: 'date',
  dateType: 'datetimeUtc',
  timeZone: pagoPago,
}

test('the component tests run in Pacific/Kiritimati, so that a date of the browser zone is not the UTC date', () => {
  expect(browserTimeZone()).toBe('Pacific/Kiritimati')
})

test.each([
  ['America/Vancouver', 'Pacific Time (Canada)'],
  ['America/Edmonton', 'Mountain Time (Canada)'],
])('names the zone %s in words, with no abbreviation', async (zone, label) => {
  await render(<DateFilter config={{ ...startsAt, timeZone: parseTimeZone(zone) }} surface="submenu" onSubmit={vi.fn()} />)

  await expect.element(page.getByRole('textbox', { name: label, exact: true })).toBeVisible()
})

test('takes today and reads the typed time in the zone of the place, not of the browser', async () => {
  // Arrange
  vi.useFakeTimers({ toFake: ['Date'] })
  // 10:30 UTC on August 15 is 23:30 on August 14 in Pago Pago, and August 16 in Kiritimati.
  vi.setSystemTime(new Date('2026-08-15T10:30:00Z'))
  const onSubmit = vi.fn()
  try {
    await render(<DateFilter config={startsAt} surface="submenu" onSubmit={onSubmit} />)

    // Act
    await page.getByRole('button', { name: 'Today', exact: true }).click()
    await page.getByRole('textbox', { name: 'American Samoa Time' }).fill('09:00')
    await page.getByRole('button', { name: 'Add Filter' }).click()

    // Assert
    await expect.element(page.getByRole('group', { name: 'Date' })).toHaveTextContent('8/14/2026')
    const value = onSubmit.mock.lastCall?.[0].value as DateValue
    expect(value.startDate).toEqual(new Date('2026-08-14T20:00:00Z'))
    expect(value.timeZone).toBe(pagoPago)
  } finally {
    vi.useRealTimers()
  }
})

test('shows a filter to edit at its date and time in the zone of the place', async () => {
  // Arrange
  const value: DateValue = { mode: 'on', startDate: new Date('2026-08-14T20:00:00Z'), dateType: 'datetimeUtc', timeZone: pagoPago }

  // Act
  await render(
    <DateFilter
      config={startsAt}
      surface="submenu"
      initialFilter={{ id: '1', ...startsAt, operator: Operators.EQUALS, value }}
      onSubmit={() => {}}
    />,
  )

  // Assert
  await expect.element(page.getByRole('group', { name: 'Date' })).toHaveTextContent('8/14/2026')
  await expect.element(page.getByRole('textbox', { name: 'American Samoa Time' })).toHaveValue('09:00')
})

test('keeps the time of a filter to edit when the config has no zone', async () => {
  // Arrange
  const value: DateValue = { mode: 'on', startDate: new Date('2026-08-14T20:00:00Z'), dateType: 'datetimeUtc', timeZone: pagoPago }
  const config = { ...startsAt, timeZone: undefined }
  const onSubmit = vi.fn()
  await render(
    <DateFilter
      config={config}
      surface="submenu"
      initialFilter={{ id: '1', ...config, operator: Operators.EQUALS, value }}
      onSubmit={onSubmit}
    />,
  )

  // Act
  await page.getByRole('button', { name: 'Update Filter' }).click()

  // Assert
  const submitted = onSubmit.mock.lastCall?.[0].value as DateValue
  expect(submitted.startDate).toEqual(value.startDate)
  expect(submitted.timeZone).toBe(pagoPago)
})

test('names the zone that a filter to edit reads its time in, not the zone of the config', async () => {
  // Arrange
  const value: DateValue = { mode: 'on', startDate: new Date('2026-08-14T20:00:00Z'), dateType: 'datetimeUtc', timeZone: pagoPago }
  const config = { ...startsAt, timeZone: parseTimeZone('America/Chicago') }

  // Act
  await render(
    <DateFilter
      config={config}
      surface="submenu"
      initialFilter={{ id: '1', ...config, operator: Operators.EQUALS, value }}
      onSubmit={() => {}}
    />,
  )

  // Assert
  await expect.element(page.getByRole('textbox', { name: 'American Samoa Time' })).toHaveValue('09:00')
})

const scheduledAt: FilterConfig = {
  propertyKey: 'scheduledAt',
  propertyLabel: 'Scheduled At',
  controlType: 'date',
  dateType: 'datetime',
}

test('keeps a datetime as the wall clock time that the user typed', async () => {
  // Arrange
  const onSubmit = vi.fn()
  await render(<DateFilter config={scheduledAt} surface="submenu" onSubmit={onSubmit} />)

  // Act
  await page.getByRole('spinbutton', { name: 'month, Date' }).click()
  await userEvent.keyboard('08152026')
  await page.getByRole('textbox', { name: 'Time' }).fill('23:30')
  await page.getByRole('button', { name: 'Add Filter' }).click()

  // Assert
  const value = onSubmit.mock.lastCall?.[0].value as DateValue
  expect(value.startDate).toEqual(new CalendarDateTime(2026, 8, 15, 23, 30))
})

test('shows a datetime to edit at its wall clock time, and keeps that time', async () => {
  // Arrange
  const value: DateValue = { mode: 'on', startDate: new CalendarDateTime(2026, 8, 15, 23, 30), dateType: 'datetime' }
  const onSubmit = vi.fn()
  await render(
    <DateFilter
      config={scheduledAt}
      surface="submenu"
      initialFilter={{ id: '1', ...scheduledAt, operator: Operators.EQUALS, value }}
      onSubmit={onSubmit}
    />,
  )

  // Act
  await page.getByRole('button', { name: 'Update Filter' }).click()

  // Assert
  await expect.element(page.getByRole('group', { name: 'Date' })).toHaveTextContent('8/15/2026')
  await expect.element(page.getByRole('textbox', { name: 'Time' })).toHaveValue('23:30')
  expect((onSubmit.mock.lastCall?.[0].value as DateValue).startDate).toEqual(value.startDate)
})

test('keeps a datetime range as two wall clock times, and names the zone of each time input', async () => {
  // Arrange
  const config = { ...scheduledAt, timeZone: pagoPago }
  const onSubmit = vi.fn()
  await render(<DateFilter config={config} surface="submenu" onSubmit={onSubmit} />)

  // Act
  await page.getByRole('button', { name: 'Between', exact: true }).click()
  await page.getByRole('spinbutton', { name: 'month, Start date' }).click()
  await userEvent.keyboard('08152026')
  await page.getByRole('spinbutton', { name: 'month, End date' }).click()
  await userEvent.keyboard('08162026')
  await page.getByRole('textbox', { name: 'Start, American Samoa Time' }).fill('01:00')
  await page.getByRole('textbox', { name: 'End, American Samoa Time' }).fill('02:30')
  await page.getByRole('button', { name: 'Add Filter' }).click()

  // Assert
  const value = onSubmit.mock.lastCall?.[0].value as DateValue
  expect(value.startDate).toEqual(new CalendarDateTime(2026, 8, 15, 1))
  expect(value.endDate).toEqual(new CalendarDateTime(2026, 8, 16, 2, 30))
})

test('refuses a datetime of a place at a time that the clocks of the place skip', async () => {
  // Arrange
  const config = { ...scheduledAt, timeZone: parseTimeZone('America/New_York') }
  await render(<DateFilter config={config} surface="submenu" onSubmit={() => {}} />)

  // Act
  await page.getByRole('spinbutton', { name: 'month, Date' }).click()
  await userEvent.keyboard('03082026')
  await page.getByRole('textbox', { name: 'Eastern Time' }).fill('02:30')

  // Assert
  await expect.element(page.getByRole('alert')).toBeVisible()
  await expect.element(page.getByRole('button', { name: 'Add Filter' })).toBeDisabled()
})

test('refuses a time that the clocks of the place skip in the spring', async () => {
  // Arrange
  const newYork = parseTimeZone('America/New_York')
  await render(<DateFilter config={{ ...startsAt, timeZone: newYork }} surface="submenu" onSubmit={() => {}} />)

  // Act
  await page.getByRole('spinbutton', { name: 'month, Date' }).click()
  await userEvent.keyboard('03082026')
  await page.getByRole('textbox', { name: 'Eastern Time' }).fill('02:30')

  // Assert
  await expect.element(page.getByRole('alert')).toHaveTextContent(
    'This time does not exist in Eastern Time on that day, because the clocks go forward. Choose another time.',
  )
  await expect.element(page.getByRole('button', { name: 'Add Filter' })).toBeDisabled()
})

test('labels the time of a filter with no zone the same way when the user adds it and when the user edits it', async () => {
  // Arrange
  const publishedAt: FilterConfig = { propertyKey: 'publishedAt', propertyLabel: 'Published At', controlType: 'date', dateType: 'datetimeUtc' }
  const onSubmit = vi.fn()
  const screen = await render(<DateFilter config={publishedAt} surface="submenu" onSubmit={onSubmit} />)
  await page.getByRole('button', { name: 'Today', exact: true }).click()
  await page.getByRole('textbox', { name: 'Time', exact: true }).fill('09:00')
  await page.getByRole('button', { name: 'Add Filter' }).click()
  const value = onSubmit.mock.lastCall?.[0].value as DateValue
  await screen.unmount()

  // Act
  await render(
    <DateFilter
      config={publishedAt}
      surface="submenu"
      initialFilter={{ id: '1', ...publishedAt, operator: Operators.EQUALS, value }}
      onSubmit={() => {}}
    />,
  )

  // Assert
  await expect.element(page.getByRole('textbox', { name: 'Time', exact: true })).toHaveValue('09:00')
})
