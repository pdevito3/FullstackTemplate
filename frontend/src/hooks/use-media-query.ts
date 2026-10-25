import * as React from "react"

/** Reads the media query on each render, the first render too, and renders again when it changes. */
export function useMediaQuery(query: string) {
  // useSyncExternalStore subscribes again each time `subscribe` changes.
  const subscribe = React.useCallback(
    (onChange: () => void) => {
      const result = matchMedia(query)
      result.addEventListener("change", onChange)
      return () => result.removeEventListener("change", onChange)
    },
    [query]
  )

  return React.useSyncExternalStore(subscribe, () => matchMedia(query).matches)
}
