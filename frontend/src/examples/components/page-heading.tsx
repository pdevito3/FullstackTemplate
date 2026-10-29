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

  // After a navigation the root puts the focus on the page content, which holds this heading. A heading that loads
  // after that takes the focus from the content, but not from a control in it.
  useEffect(() => {
    const heading = headingRef.current
    const active = document.activeElement
    if (heading && (!active || active === document.body || active.contains(heading))) {
      heading.focus()
    }
  }, [headingRef])

  return (
    <h1 ref={headingRef} tabIndex={-1} className={cn('text-3xl font-bold tracking-tight outline-none', className)}>
      {children}
    </h1>
  )
}
