import { CalendarDateTime } from '@internationalized/date'
import { describe, expect, it } from 'vitest'

import { latestOverEdits } from './latest-over-edits'

const FIELDS = ['title', 'startsAt', 'capacity'] as const
const loaded = { title: 'Intro', startsAt: new CalendarDateTime(2026, 6, 1, 10), capacity: 20 }

describe('latestOverEdits', () => {
  it('keeps each edit of a field that the latest record did not change', () => {
    const edited = { ...loaded, title: 'Intro to Kubb' }
    const latest = { ...loaded, capacity: 30 }

    expect(latestOverEdits(loaded, edited, latest, FIELDS)).toEqual({ changed: ['capacity'], kept: ['title'] })
  })

  it('takes the latest value of a field that both the user and the latest record changed', () => {
    const edited = { ...loaded, title: 'Intro to Kubb' }
    const latest = { ...loaded, title: 'Kubb for teams' }

    expect(latestOverEdits(loaded, edited, latest, FIELDS)).toEqual({ changed: ['title'], kept: [] })
  })

  it('compares a date by its value, not by the object', () => {
    const edited = { ...loaded, startsAt: new CalendarDateTime(2026, 6, 2, 10) }
    const latest = { ...loaded, startsAt: new CalendarDateTime(2026, 6, 1, 10) }

    expect(latestOverEdits(loaded, edited, latest, FIELDS)).toEqual({ changed: [], kept: ['startsAt'] })
  })

  it('takes only values that compare by their text', () => {
    const picked = { place: { id: 'a', name: 'Hall' } }

    // @ts-expect-error An object field compares as "[object Object]", so the merge would drop the edit of the user.
    latestOverEdits(picked, picked, picked, ['place'])
  })
})
