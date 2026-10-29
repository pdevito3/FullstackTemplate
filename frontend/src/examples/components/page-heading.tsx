import { useEffect, useRef, type ReactNode, type RefObject } from 'react'

import { cn } from '@/lib/utils'

interface PageHeadingProps {
  children: ReactNode
  className?: string
  /** Give a ref to move the focus to the heading later, for example after an action removes its own button. */
  ref?: RefObject<HTMLHeadingElement | null>
}

/**
 * The h1 of a page. When it shows while the focus is on the body, it takes the focus. That is the case after a save
 * that opened this page, because the Save button went away with the page before. A screen reader then reads the new
 * page, and Tab goes on from the heading.
 */
export function PageHeading({ children, className, ref }: PageHeadingProps) {
  const ownRef = useRef<HTMLHeadingElement>(null)
  const headingRef = ref ?? ownRef

  useEffect(() => {
    if (!document.activeElement || document.activeElement === document.body) {
      headingRef.current?.focus()
    }
  }, [headingRef])

  return (
    <h1 ref={headingRef} tabIndex={-1} className={cn('text-3xl font-bold tracking-tight outline-none', className)}>
      {children}
    </h1>
  )
}
