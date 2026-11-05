/**
 * Waits two frames, so that each IntersectionObserver that already watches the page has had its call. The browser
 * checks the observers in a frame and calls them before the next frame, in no fixed order, so a second observer
 * proves nothing.
 */
export function observersHaveRun() {
  return new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())))
}
