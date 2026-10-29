import { useEffect, useRef, type ReactNode, type RefObject } from 'react'

import { cn } from '@/lib/utils'

interface PageHeadingProps {
  children: ReactNode
  className?: string
  ref?: RefObject<HTMLHeadingElement | null>
}

export function PageHeading({ children, className, ref }: PageHeadingProps) {
  const ownRef = useRef<HTMLHeadingElement>(null)
  const headingRef = ref ?? ownRef

  useEffect(() => {
    const heading = headingRef.current
    if (heading && focusIsAboveOrOn(heading)) {
      heading.focus()
    }
  }, [headingRef])

  return (
    <h1 ref={headingRef} tabIndex={-1} className={cn('text-3xl font-bold tracking-tight outline-none', className)}>
      {children}
    </h1>
  )
}

function focusIsAboveOrOn(heading: HTMLElement) {
  const active = document.activeElement
  return !active || active === document.body || active.contains(heading)
}
