import { useEffect, useRef } from 'react'

export function useDebouncedCallback<A extends unknown[]>(callback: (...args: A) => void, delayMs = 300) {
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined)

  useEffect(() => () => clearTimeout(timer.current), [])

  return (...args: A) => {
    clearTimeout(timer.current)
    timer.current = setTimeout(() => callback(...args), delayMs)
  }
}
