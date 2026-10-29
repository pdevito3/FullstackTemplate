import { useEffect, useRef } from 'react'

/** Calls `callback` `delayMs` after the last call of the function that it returns. An unmount drops the call. */
export function useDebouncedCallback<A extends unknown[]>(callback: (...args: A) => void, delayMs = 300) {
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined)

  useEffect(() => () => clearTimeout(timer.current), [])

  return (...args: A) => {
    clearTimeout(timer.current)
    timer.current = setTimeout(() => callback(...args), delayMs)
  }
}
