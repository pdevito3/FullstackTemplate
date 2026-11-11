import { describe, expect, it } from 'vitest'
import { moveSafeArea, safeAreaFrom, type Point } from './safe-area'

// A trigger on the left, and a tall submenu beside it that reaches far above it.
const trigger = { left: 0, top: 300, right: 200, bottom: 330 }
const submenu = { left: 210, top: 0, right: 600, bottom: 400 }
const start = { x: 20, y: 315 }

function walk(from: Point, to: Point, steps = 50) {
  let area = moveSafeArea(safeAreaFrom(trigger), from, trigger, submenu)
  const areas = []
  for (let step = 1; step <= steps; step++) {
    const point = { x: from.x + ((to.x - from.x) * step) / steps, y: from.y + ((to.y - from.y) * step) / steps }
    area = moveSafeArea(area, point, trigger, submenu)
    areas.push({ point, area })
  }
  return areas
}

describe('moveSafeArea', () => {
  for (const target of [
    { name: 'the top corner of the submenu', point: { x: 211, y: 1 } },
    { name: 'a field far above the trigger', point: { x: 260, y: 40 } },
    { name: 'a field below the trigger', point: { x: 400, y: 390 } },
  ]) {
    it(`keeps the area on each point of a straight line to ${target.name}, until the mouse is in the submenu`, () => {
      const outside = walk(start, target.point).filter(({ point }) => point.x < submenu.left)

      expect(outside.filter(({ area }) => area === null)).toEqual([])
    })
  }

  it('ends the area when the mouse reaches the submenu', () => {
    const [last] = walk(start, { x: 300, y: 200 }).slice(-1)

    expect(last.area).toBeNull()
  })

  it('ends the area when the mouse moves straight up, away from the submenu', () => {
    const [last] = walk(start, { x: 20, y: 200 }).slice(-1)

    expect(last.area).toBeNull()
  })

  it('ends the area when the mouse moves straight down to the next item', () => {
    const [last] = walk(start, { x: 20, y: 345 }).slice(-1)

    expect(last.area).toBeNull()
  })

  it('does not start an area off the trigger once it ended', () => {
    expect(moveSafeArea(null, { x: 205, y: 200 }, trigger, submenu)).toBeNull()
  })

  it('starts the area again when the mouse comes back to the trigger', () => {
    expect(moveSafeArea(null, { x: 50, y: 310 }, trigger, submenu)).toEqual({
      from: { left: 50, top: 310, right: 50, bottom: 310 },
      isOnTrigger: true,
    })
  })

  it('marks the area as off the trigger while the mouse crosses the other items', () => {
    const area = moveSafeArea(safeAreaFrom(trigger), start, trigger, submenu)

    expect(moveSafeArea(area, { x: 40, y: 290 }, trigger, submenu)).toMatchObject({ isOnTrigger: false })
  })
})
