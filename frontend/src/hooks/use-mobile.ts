import { useMediaQuery } from "@/hooks/use-media-query"

/** True below the `md` breakpoint of Tailwind. */
export function useIsMobile() {
  return useMediaQuery("(width < 48rem)")
}
