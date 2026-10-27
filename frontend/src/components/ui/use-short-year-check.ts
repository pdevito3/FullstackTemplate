import { useEffect, useRef, useState } from "react"
import type { DateValue as AriaDateValue } from "react-aria-components"

// A date field reports each year as the user types it (2, 20, 202, 2026), so a year below 1000 is still being typed.
export function isTypedInFull(date: AriaDateValue | null | undefined): date is AriaDateValue {
  return date != null && date.year >= 1000
}

export const shortYearMessage = "Enter a 4-digit year."

// A year below 1000 is still being typed while the field has focus. After the focus leaves, it is an error until the
// year has four digits or the field is empty.
export function useShortYearCheck(date: AriaDateValue | null) {
  const isShort = date != null && !isTypedInFull(date)
  const [isMarked, setIsMarked] = useState(false)
  if (isMarked && !isShort) setIsMarked(false)

  const isPressingRef = useRef(false)
  useEffect(() => {
    const handlePointerDown = () => { isPressingRef.current = true }
    const handlePointerUp = () => { isPressingRef.current = false }
    document.addEventListener("pointerdown", handlePointerDown, true)
    document.addEventListener("pointerup", handlePointerUp, true)
    document.addEventListener("pointercancel", handlePointerUp, true)
    // A context menu takes the pointerup, so the press ends when the menu opens.
    document.addEventListener("contextmenu", handlePointerUp, true)
    return () => {
      document.removeEventListener("pointerdown", handlePointerDown, true)
      document.removeEventListener("pointerup", handlePointerUp, true)
      document.removeEventListener("pointercancel", handlePointerUp, true)
      document.removeEventListener("contextmenu", handlePointerUp, true)
    }
  }, [])

  const onBlur = () => {
    if (!isShort) return
    if (!isPressingRef.current) {
      setIsMarked(true)
      return
    }
    // A pointer press moves the focus on pointerdown and presses on pointerup. A message that showed between them would
    // move the pressed button down, and the press would miss. So it shows after the press.
    const pressEnd = new AbortController()
    const mark = () => {
      pressEnd.abort()
      setIsMarked(true)
    }
    document.addEventListener("pointerup", mark, { signal: pressEnd.signal })
    document.addEventListener("pointercancel", mark, { signal: pressEnd.signal })
  }

  return { isShortYear: isMarked && isShort, onBlur }
}
