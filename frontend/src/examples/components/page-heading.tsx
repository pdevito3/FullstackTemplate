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
