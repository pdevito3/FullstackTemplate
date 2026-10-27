import * as React from "react"
import {
  DialogTrigger,
  Heading,
  Popover as PopoverPrimitive,
  type DialogTriggerProps,
  type PopoverProps as PopoverPrimitiveProps,
} from "@animate-ui/react/primitives/react-aria"
import { cn } from "@/lib/utils"

function PopoverTrigger({ children, ...props }: DialogTriggerProps) {
  return (
    <DialogTrigger {...props}>
      {children}
    </DialogTrigger>
  )
}

function Popover({
  className,
  placement = "bottom",
  offset = 4,
  crossOffset = 0,
  ref,
  ...props
}: Omit<PopoverPrimitiveProps, "className"> & {
  className?: string
  ref?: React.Ref<HTMLElement>
}) {
  // React Aria places the popover again only when the popover or its trigger resizes. A popover at its max height
  // does not resize when its content grows, so it would scroll. A window resize makes React Aria place it again.
  const popoverRef = React.useCallback(
    (element: HTMLElement | null) => {
      if (!element) return
      const observer = new ResizeObserver(() => {
        if (element.scrollHeight > element.clientHeight) window.dispatchEvent(new Event("resize"))
      })
      for (const child of element.children) observer.observe(child)
      const callerCleanup = setRef(ref, element)
      return () => {
        observer.disconnect()
        if (callerCleanup) callerCleanup()
        else setRef(ref, null)
      }
    },
    [ref]
  )

  return (
    <PopoverPrimitive
      ref={popoverRef}
      data-slot="popover-content"
      placement={placement}
      offset={offset}
      crossOffset={crossOffset}
      className={cn(
        "z-50 flex w-72 origin-(--trigger-anchor-point) flex-col gap-2.5 rounded-lg bg-popover p-2.5 text-sm text-popover-foreground shadow-md ring-1 ring-foreground/10 outline-hidden",
        className
      )}
      {...props}
    />
  )
}

function setRef<T>(ref: React.Ref<T> | undefined, value: T | null): (() => void) | void {
  if (typeof ref === "function") return ref(value)
  if (ref) ref.current = value
}

function PopoverHeader({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="popover-header"
      className={cn("flex flex-col gap-0.5 text-sm", className)}
      {...props}
    />
  )
}

function PopoverTitle({
  className,
  ...props
}: React.ComponentProps<typeof Heading>) {
  return (
    <Heading
      data-slot="popover-title"
      className={cn("font-medium", className)}
      {...props}
    />
  )
}

function PopoverDescription({
  className,
  ...props
}: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="popover-description"
      className={cn("text-muted-foreground", className)}
      {...props}
    />
  )
}

export {
  Popover,
  PopoverDescription,
  PopoverHeader,
  PopoverTitle,
  PopoverTrigger,
}
