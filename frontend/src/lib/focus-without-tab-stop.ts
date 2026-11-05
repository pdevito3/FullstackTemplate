export function focusWithoutTabStop(target: HTMLElement | null) {
  if (!target) return
  if (!target.hasAttribute('tabindex')) {
    target.setAttribute('tabindex', '-1')
    target.setAttribute('data-page-focus', '')
    const removeTabStop = () => {
      target.removeAttribute('tabindex')
      target.removeAttribute('data-page-focus')
      target.removeEventListener('blur', handleBlur)
      target.removeEventListener('pointerdown', removeTabStop)
    }
    // A switch to another tab blurs the target, but the target keeps the focus for the return.
    const handleBlur = () => {
      if (document.activeElement !== target) removeTabStop()
    }
    target.addEventListener('blur', handleBlur)
    // A press inside the target must not give it the focus again.
    target.addEventListener('pointerdown', removeTabStop)
  }
  target.focus({ preventScroll: true })
}
