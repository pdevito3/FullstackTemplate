export interface Point {
  x: number
  y: number
}

export interface Box {
  left: number
  top: number
  right: number
  bottom: number
}

/**
 * The area that the mouse crosses on a straight line from its last point on the submenu trigger (`from`) to any point
 * of the submenu. While the mouse is in it, the other items of the menu must not take the hover, or they close the
 * submenu. `null` when the items take the hover.
 */
export type SafeArea = { from: Box; isOnTrigger: boolean } | null

/** The safe area while the mouse rests on the trigger and has not moved yet. */
export function safeAreaFrom(trigger: Box): SafeArea {
  return { from: trigger, isOnTrigger: true }
}

/** The safe area after the mouse moves to `point`. */
export function moveSafeArea(area: SafeArea, point: Point, trigger: Box, submenu: Box): SafeArea {
  if (contains(trigger, point)) {
    return { from: { left: point.x, top: point.y, right: point.x, bottom: point.y }, isOnTrigger: true }
  }
  if (area === null || contains(submenu, point)) return null
  return isInHull(point, [...cornersOf(area.from), ...cornersOf(submenu)]) ? { from: area.from, isOnTrigger: false } : null
}

function contains(box: Box, { x, y }: Point): boolean {
  return x >= box.left && x <= box.right && y >= box.top && y <= box.bottom
}

function cornersOf(box: Box): Point[] {
  return [
    { x: box.left, y: box.top },
    { x: box.right, y: box.top },
    { x: box.right, y: box.bottom },
    { x: box.left, y: box.bottom },
  ]
}

/** The cross product of `a -> b` and `a -> c`: positive when `c` is on the left of the line from `a` to `b`. */
function cross(a: Point, b: Point, c: Point): number {
  return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)
}

/** Whether `point` is in the smallest convex polygon that holds all `points`, edges included. */
function isInHull(point: Point, points: Point[]): boolean {
  const hull = convexHull(points)
  return hull.every((corner, index) => cross(corner, hull[(index + 1) % hull.length], point) >= 0)
}

/** The corners of the convex hull in one turning direction, by Andrew's monotone chain. */
function convexHull(points: Point[]): Point[] {
  const sorted = [...points].sort((a, b) => a.x - b.x || a.y - b.y)
  const chain = (ordered: Point[]) => {
    const result: Point[] = []
    for (const point of ordered) {
      while (result.length >= 2 && cross(result[result.length - 2], result[result.length - 1], point) <= 0) result.pop()
      result.push(point)
    }
    result.pop()
    return result
  }
  return [...chain(sorted), ...chain([...sorted].reverse())]
}
