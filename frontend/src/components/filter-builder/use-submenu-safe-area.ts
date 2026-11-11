import { useLayoutEffect, type RefObject } from 'react'
import { moveSafeArea, safeAreaFrom, type SafeArea } from './utils/safe-area'

/**
 * After this rest off the trigger, the item under the mouse takes the hover, as the user stopped there. React Aria's
 * safe triangle waits 1000 ms and then 100 ms more, so a trackpad user can lift a finger on the way.
 */
const REST_MS = 1100

/**
 * Keeps an open submenu open while the mouse moves to it over the other items of the menu. Call it in a component
 * that shows only while the submenu is open.
 *
 * React Aria has this safe triangle, but only for a `Menu` as the submenu, and it starts only after two moves of the
 * mouse. A move from the trigger to the field of a `Dialog` closed the submenu.
 */
export function useSubmenuSafeArea(triggerRef: RefObject<HTMLElement | null>, submenuRef: RefObject<HTMLElement | null>) {
  useLayoutEffect(() => {
    const trigger = triggerRef.current
    const menu = trigger?.closest<HTMLElement>('[role="menu"]')
    if (!trigger || !menu) return

    let restTimer: ReturnType<typeof setTimeout> | undefined
    // React Aria sets data-hovered for a mouse or a pen, not for a touch, so a touch does not start the area.
    let area: SafeArea = trigger.hasAttribute('data-hovered') ? safeAreaFrom(trigger.getBoundingClientRect()) : null

    // The item under the mouse did not get its pointerover while the area held, so it is told that the mouse is over it.
    const releaseTo = ({ x, y }: { x: number; y: number }) => {
      area = null
      const item = document.elementFromPoint(x, y)
      if (item && menu.contains(item)) item.dispatchEvent(new PointerEvent('pointerover', { bubbles: true, pointerType: 'mouse', clientX: x, clientY: y }))
    }

    // The browser sends pointerover and pointerout before the pointermove of the same move.
    const handlePointerMove = (event: PointerEvent) => {
      const submenu = submenuRef.current
      if (event.pointerType === 'touch' || !submenu) return
      clearTimeout(restTimer)
      const point = { x: event.clientX, y: event.clientY }
      const next = moveSafeArea(area, point, trigger.getBoundingClientRect(), submenu.getBoundingClientRect())
      if (area && !next) releaseTo(point)
      else area = next
      if (area && !area.isOnTrigger) restTimer = setTimeout(() => releaseTo(point), REST_MS)
    }

    // React Aria hovers an item on these events, and a hover on another item closes the submenu. Presses and the wheel
    // still reach the items.
    const blockHover = (event: PointerEvent) => {
      const to = event.type === 'pointerover' ? event.target : event.relatedTarget
      if (area && to instanceof Node && menu.contains(to) && !trigger.contains(to)) event.stopImmediatePropagation()
    }

    // The wheel moves the items under the mouse, so the mouse no longer goes from the trigger to the submenu.
    const handleWheel = (event: WheelEvent) => {
      clearTimeout(restTimer)
      if (area) releaseTo({ x: event.clientX, y: event.clientY })
    }

    window.addEventListener('pointerover', blockHover, true)
    window.addEventListener('pointerout', blockHover, true)
    window.addEventListener('pointermove', handlePointerMove)
    window.addEventListener('wheel', handleWheel, { passive: true })
    return () => {
      clearTimeout(restTimer)
      window.removeEventListener('pointerover', blockHover, true)
      window.removeEventListener('pointerout', blockHover, true)
      window.removeEventListener('pointermove', handlePointerMove)
      window.removeEventListener('wheel', handleWheel)
    }
  }, [triggerRef, submenuRef])
}
